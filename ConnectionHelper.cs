using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using MySqlConnector;
using Renci.SshNet;
using SerpRankingAPI.Config;

namespace MySQLConfigurationAndSsh;

public static class ConnectionHelper
{
    private static SshClient _sshClient;

    public static event EventHandler<TupleEventArgs<string, List<Process>>> ShowMessageBoxEvent;

    public static string GetIPAddress()
    {
        var address = string.Empty;
        var request = WebRequest.Create("http://checkip.dyndns.org/");
        using (var response = request.GetResponse())
        using (var stream = new StreamReader(response.GetResponseStream()))
        {
            address = stream.ReadToEnd();
        }

        var first = address.IndexOf("Address: ", StringComparison.Ordinal) + 9;
        var last = address.LastIndexOf("</body>", StringComparison.Ordinal);
        return address.Substring(first, last - first);
    }

    public static async Task<string> GetPublicIPAddressAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetStringAsync("https://api.ipify.org").ConfigureAwait(false);
            return response.Trim();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Impossibile determinare l'IP pubblico.", ex);
        }
    }

    public static IPAddress GetLocalIPv4AddressRequiresInternet()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address;
            }

            throw new InvalidOperationException("Impossibile determinare l'EndPoint locale.");
        }
        catch (SocketException ex)
        {
            Debug.WriteLine($"[GetLocalIPv4] SocketException: {ex.Message}");
            return IPAddress.None;
        }
    }

    public static bool IsPortInUse(uint boundPort)
    {
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpListeners = ipProperties.GetActiveTcpListeners();
        return tcpListeners.Any(endpoint => endpoint.Port == boundPort);
    }

    public static void CheckAndHandlePortIsUsed()
    {
        if (_sshClient?.IsConnected == true)
        {
            return;
        }

        var (processesUsingPort, debugMessage) = CheckProcessUsingPort(3306);
        if (!processesUsingPort.Any())
        {
            return;
        }

        var text = $"Un altro programma sta utilizzando la porta 3306. Vuoi terminarlo?\n{debugMessage}";
        ShowMessageBoxEvent?.Invoke(typeof(ConnectionHelper), TupleEventArgs.Create(text, processesUsingPort));
    }

    public static (List<Process> processes, string debugMessage) CheckProcessUsingPort(uint boundPort)
    {
        var used = ProcessPorts.ProcessPortMap.FindAll(x => x.PortNumber == boundPort);
        if (!used.Any())
        {
            return (new List<Process>(), string.Empty);
        }

        var message = used.Select(s => s.ProcessPortDescription)
            .Aggregate((a, b) => a + Environment.NewLine + b);

        var processesUsingPort = (from proc in Process.GetProcesses()
                join procPort in used on proc.Id equals procPort.ProcessId
                select proc)
            .ToList();

        return (processesUsingPort, message);
    }

    public static bool CheckIfPortIsUsed(uint boundPort)
    {
        return IsPortInUse(boundPort);
    }

    public static void KillProcess(List<Process> processToKill)
    {
        KillProcesses(processToKill);
    }

    public static void KillProcesses(IEnumerable<Process> processesToKill)
    {
        foreach (var process in processesToKill)
        {
            try
            {
                process.Kill();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Impossibile terminare il processo {process.ProcessName}. Errore: {ex.Message}");
            }
        }
    }

    public static bool EnableSshIfPossible(uint boundPort)
    {
        var selectedWebsite = WebSiteConfiguration.SelectedWebsite;
        var credentials = selectedWebsite?.SshCredentials;
        if (credentials == null || string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
        {
            return false;
        }

        try
        {
            if (_sshClient?.IsConnected == true)
            {
                return true;
            }

            _sshClient?.Dispose();
            _sshClient = WebSiteConfiguration.SelectedWebsiteSsh;
            _sshClient.Connect();
            if (!_sshClient.IsConnected)
            {
                return false;
            }

            var forwardedPort = new ForwardedPortLocal("localhost", boundPort, "localhost", boundPort);
            _sshClient.AddForwardedPort(forwardedPort);
            forwardedPort.Start();

            selectedWebsite.MySql.Host = "localhost";
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Errore nella connessione SSH: {ex.Message}");
            _sshClient?.Disconnect();
            return false;
        }
    }

    public static bool CheckDbConnection(MySqlConnection mySqlConnection = null)
    {
        try
        {
            mySqlConnection ??= WebSiteConfiguration.SelectedWebsite.MySql.ShortTimeoutConnection;
            mySqlConnection.Open();
            mySqlConnection.Close();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> CheckDbConnectionAsync(MySqlConnection mySqlConnection = null)
    {
        try
        {
            mySqlConnection ??= WebSiteConfiguration.SelectedWebsite.MySql.ShortTimeoutConnection;
            await mySqlConnection.OpenAsync().ConfigureAwait(false);
            await mySqlConnection.CloseAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
