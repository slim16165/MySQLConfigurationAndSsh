using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using MySQLConfigurationAndSsh.Config;
using Renci.SshNet;
using SerpRankingAPI.Config;

namespace MySQLConfigurationAndSsh;

/// <summary>
/// Owns the one active desktop database tunnel. Database credentials remain in the
/// original site configuration; forwarding never changes its host or port.
/// </summary>
public static class ConnectionHelper
{
    private static readonly object Sync = new();
    private static WebsiteAppConfigBase _activeSite;
    private static SshClient _sshClient;
    private static ForwardedPortLocal _forward;
    private static Process _openSsh;
    private static uint _localPort;

    // Sanitized diagnostic for UI and support; never contains SSH credentials or hostnames.
    public static string LastTunnelFailureCode { get; private set; } = "";

    public static bool RequiresTunnel(WebsiteAppConfigBase site)
    {
        var ssh = site?.SshCredentials;
        return ssh != null && (!string.IsNullOrWhiteSpace(ssh.OpenSshAlias) ||
            (!string.IsNullOrWhiteSpace(ssh.Username) && !string.IsNullOrWhiteSpace(ssh.Password)));
    }

    public static bool EnableSshIfPossible(uint boundPort = 3306)
        => EnableSshIfPossible(WebSiteConfiguration.SelectedWebsite);

    public static bool EnableSshIfPossible(WebsiteAppConfigBase site)
    {
        if (!RequiresTunnel(site))
        {
            CloseTunnel();
            return false;
        }

        lock (Sync)
        {
            if (ReferenceEquals(site, _activeSite) && TunnelIsAlive())
                return true;

            CloseTunnelCore();
            LastTunnelFailureCode = "";
            try
            {
                var alias = site.SshCredentials.OpenSshAlias;
                _localPort = AllocateLoopbackPort();
                if (!string.IsNullOrWhiteSpace(alias))
                    StartOpenSsh(alias, site.MySql.Port > 0 ? site.MySql.Port : 3306);
                else
                    StartSshNet(site);
                _activeSite = site;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Database tunnel failed: " + ex.GetType().Name);
                CloseTunnelCore();
                LastTunnelFailureCode = ex is TimeoutException ? "SSH_FORWARD_TIMEOUT"
                    : ex is InvalidOperationException && ex.Message.Contains("exited before forwarding")
                        ? "SSH_EARLY_EXIT"
                    : ex is System.ComponentModel.Win32Exception win32 ? "SSH_PROCESS_START_" + win32.NativeErrorCode
                    : ex.GetType().Name;
                return false;
            }
        }
    }

    public static MySqlConnection CreateConnection(WebsiteAppConfigBase site, bool shortTimeout = false)
    {
        if (site == null) throw new InvalidOperationException("Select a website first.");
        lock (Sync)
        {
            if (RequiresTunnel(site))
            {
                if (!ReferenceEquals(site, _activeSite) || !TunnelIsAlive())
                    throw new InvalidOperationException("No active SSH tunnel for the selected website.");
                var connection = site.MySql.CreateConnection("127.0.0.1", (int)_localPort,
                    shortTimeout ? 20 : (int?)null);
                // SSH already encrypts the loopback tunnel; MySQL TLS negotiation fails
                // against current IONOS MySQL when used from the .NET Framework client.
                var options = new MySqlConnectionStringBuilder(connection.ConnectionString)
                {
                    SslMode = MySqlSslMode.Disabled
                };
                connection.ConnectionString = options.ConnectionString;
                return connection;
            }

            var host = site.MySql.Host;
            if (host != "localhost" && host != "127.0.0.1" && host != "::1")
                throw new InvalidOperationException("Remote database requires a tunnel.");
            return site.MySql.CreateConnection(timeoutSeconds: shortTimeout ? 5 : (int?)null);
        }
    }

    public static async Task<bool> CheckDbConnectionAsync(MySqlConnection connection = null)
    {
        try
        {
            using (var db = connection ?? CreateConnection(WebSiteConfiguration.SelectedWebsite, true))
            {
                await db.OpenAsync().ConfigureAwait(false);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Database connection failed: " + ex.GetType().Name);
            return false;
        }
    }

    public static bool CheckDbConnection(MySqlConnection connection = null)
        => CheckDbConnectionAsync(connection).GetAwaiter().GetResult();

    public static void CloseTunnel()
    {
        lock (Sync)
            CloseTunnelCore();
    }

    private static uint AllocateLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return (uint)((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static void StartSshNet(WebsiteAppConfigBase site)
    {
        var credentials = site.SshCredentials;
        _sshClient = new SshClient(site.MySql.Host, credentials.Port ?? 22,
            credentials.Username, credentials.Password);
        _sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(12);
        _sshClient.Connect();
        _forward = new ForwardedPortLocal("127.0.0.1", _localPort, "127.0.0.1",
            (uint)(site.MySql.Port > 0 ? site.MySql.Port : 3306));
        _sshClient.AddForwardedPort(_forward);
        _forward.Start();
        if (!_forward.IsStarted)
            throw new InvalidOperationException("SSH.NET local forward did not start.");
    }

    private static void StartOpenSsh(string alias, int remotePort)
    {
        if (!Regex.IsMatch(alias, @"\A[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}\z"))
            throw new InvalidOperationException("Invalid OpenSSH host alias.");

        var command = new ProcessStartInfo
        {
            FileName = GetOpenSshExecutable(),
            Arguments = "-N -T -o BatchMode=yes -o ExitOnForwardFailure=yes " +
                        "-o StrictHostKeyChecking=yes -o ConnectTimeout=30 " +
                        "-o ServerAliveInterval=30 -o ServerAliveCountMax=2 " +
                        "-L 127.0.0.1:" + _localPort + ":127.0.0.1:" + remotePort + " " + alias,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _openSsh = Process.Start(command) ??
            throw new InvalidOperationException("Cannot launch OpenSSH.");
        var startup = Stopwatch.StartNew();
        while (startup.Elapsed < TimeSpan.FromSeconds(75))
        {
            if (_openSsh.HasExited)
                throw new InvalidOperationException("OpenSSH exited before forwarding.");
            // A listening port alone does not prove SSH can reach the remote MySQL.
            if (PortAcceptsConnections(_localPort) && PortReturnsMySqlGreeting(_localPort))
                return;
            Thread.Sleep(300);
        }
        throw new TimeoutException("OpenSSH forwarding did not reach MySQL.");
    }

    private static string GetOpenSshExecutable()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var systemSsh = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
            if (File.Exists(systemSsh)) return systemSsh;

            // A 32-bit process must use Sysnative to access the 64-bit OpenSSH binary.
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                var sysnativeSsh = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows), "Sysnative", "OpenSSH", "ssh.exe");
                if (File.Exists(sysnativeSsh)) return sysnativeSsh;
            }
        }
        return "ssh";
    }

    private static bool PortAcceptsConnections(uint port)
    {
        using (var tcp = new TcpClient())
        {
            try
            {
                var result = tcp.BeginConnect(IPAddress.Loopback, (int)port, null, null);
                using (result.AsyncWaitHandle)
                {
                    if (!result.AsyncWaitHandle.WaitOne(150)) return false;
                    tcp.EndConnect(result);
                    return tcp.Connected;
                }
            }
            catch (SocketException) { return false; }
        }
    }

    private static bool PortReturnsMySqlGreeting(uint port)
    {
        using var tcp = new TcpClient();
        try
        {
            var pending = tcp.BeginConnect(IPAddress.Loopback, (int)port, null, null);
            using (pending.AsyncWaitHandle)
            {
                if (!pending.AsyncWaitHandle.WaitOne(300)) return false;
                tcp.EndConnect(pending);
            }

            tcp.ReceiveTimeout = 4000;
            using var stream = tcp.GetStream();
            var greeting = new byte[5];
            var read = 0;
            while (read < greeting.Length)
            {
                var count = stream.Read(greeting, read, greeting.Length - read);
                if (count == 0) return false;
                read += count;
            }
            return greeting[3] == 0 && greeting[4] == 10;
        }
        catch (IOException) { return false; }
        catch (SocketException) { return false; }
    }

    private static bool TunnelIsAlive()
    {
        if (_localPort == 0) return false;
        if (_openSsh != null) return !_openSsh.HasExited && PortAcceptsConnections(_localPort);
        return _sshClient?.IsConnected == true && _forward?.IsStarted == true;
    }

    private static void CloseTunnelCore()
    {
        try { _forward?.Stop(); } catch (Exception) { }
        try { _sshClient?.Disconnect(); } catch (Exception) { }
        _forward?.Dispose();
        _sshClient?.Dispose();
        _forward = null;
        _sshClient = null;
        if (_openSsh != null)
        {
            try
            {
                if (!_openSsh.HasExited) _openSsh.Kill();
                _openSsh.WaitForExit(1500);
            }
            catch (Exception) { }
            _openSsh.Dispose();
            _openSsh = null;
        }
        _localPort = 0;
        _activeSite = null;
    }
}