using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using MySQLConfigurationAndSsh.Config;
using MySqlConnector;
using Renci.SshNet;

namespace MySQLConfigurationAndSsh;

/// <summary>
/// Classe helper per la gestione della connessione a MySQL via SSH (tunnel) o in locale,
/// con funzioni di utilità per la risoluzione IP, verifica porte, e kill dei processi concorrenti.
/// </summary>
public static class ConnectionHelper
{
    private static SshClient _sshClient;

    /// <summary>
    /// Evento invocato quando si rileva che la porta è già in uso
    /// e si richiede all'utente (tramite UI) se vuole killare il processo in conflitto.
    /// </summary>
    public static event EventHandler<TupleEventArgs<string, List<Process>>> ShowMessageBoxEvent;

    #region IP / Network Utility

    /// <summary>
    /// Restituisce l'IP pubblico del chiamante, usando un servizio HTTP esterno.
    /// </summary>
    /// <remarks>
    /// Usa HttpClient per evitare WebRequest, disabilitando l'uso sincrono.
    /// </remarks>
    public static async Task<string> GetPublicIPAddressAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            // Esempio di URL che restituisce l'IP in plain text
            // NB: "http://checkip.dyndns.org/" restituisce HTML, da parsare.
            // Meglio un servizio che dia ip in forma pulita, es. "https://api.ipify.org"
            string response = await httpClient.GetStringAsync("https://api.ipify.org");
            return response.Trim();
        }
        catch (Exception ex)
        {
            // Log, se necessario, e poi solleva un'eccezione specializzata
            throw new InvalidOperationException("Impossibile determinare l'IP pubblico.", ex);
        }
    }

    /// <summary>
    /// Restituisce l'indirizzo IPv4 locale che verrebbe utilizzato per uscire su Internet.
    /// </summary>
    /// <remarks>
    /// Collega un socket UDP a 8.8.8.8 (DNS di Google) per determinare l'IP locale in uso.
    /// </remarks>
    public static IPAddress GetLocalIPv4AddressRequiresInternet()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Porta "fittizia": 65530 è una porta alta casuale
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address;
            }
            throw new InvalidOperationException("Impossibile determinare l'EndPoint locale.");
        }
        catch (SocketException ex)
        {
            // Eventuale log
            Debug.WriteLine($"[GetLocalIPv4] SocketException: {ex.Message}");
            // Ritorno IPAddress.None come fallback
            return IPAddress.None;
        }
    }

    #endregion

    #region Check e Gestione Porte

    /// <summary>
    /// Verifica se la porta specificata (boundPort) è già in uso su localhost.
    /// </summary>
    public static bool IsPortInUse(uint boundPort)
    {
        // Uso le API .NET in modo da non dover fare parsing di netstat
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpListeners = ipProperties.GetActiveTcpListeners();
        return tcpListeners.Any(endpoint => endpoint.Port == boundPort);
    }

    /// <summary>
    /// Se la porta 3306 risulta già in uso, scatena l'evento ShowMessageBoxEvent
    /// per chiedere all'utente se intende killare il processo che la occupa.
    /// </summary>
    /// <remarks>
    /// Se _sshClient è già connesso, non effettua alcun controllo, assumendo che il tunnel SSH sia già attivo.
    /// </remarks>
    public static void CheckAndHandlePortIsUsed()
    {
        // Controllo se c'è già un SSH tunnel stabilito
        if (_sshClient?.IsConnected == true)
            return;
        
        if (CheckIfPortIsUsed(3306))
        {
            var (processesUsingPort, debugMessage) = CheckProcessUsingPort(3306);

            string text = $"Un altro programma sta utilizzando la porta 3306. Vuoi terminarlo?\n{debugMessage}";
            // Invoca l'evento, così che la UI possa gestire la richiesta
            ShowMessageBoxEvent?.Invoke(typeof(ConnectionHelper), TupleEventArgs.Create(text, processesUsingPort));
        }
    }

    /// <summary>
    /// Ritorna l'elenco dei processi che stanno usando la porta specificata,
    /// nonché una stringa di debug che elenca i dettagli dei processi.
    /// </summary>
    public static (List<Process> processes, string debugMessage) CheckProcessUsingPort(uint boundPort)
    {
        List<ProcessPort> used = ProcessPorts.ProcessPortMap.FindAll(x => x.PortNumber == boundPort);
        if (!used.Any()) return (new List<Process>(), "");

        // Crea un "elenco di debug" con i dettagli
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
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpListeners = ipProperties.GetActiveTcpListeners();
        return tcpListeners.Any(endpoint => endpoint.Port == boundPort);
    }

    /// <summary>
    /// Termina tutti i processi indicati nella lista.
    /// </summary>
    public static void KillProcesses(IEnumerable<Process> processesToKill)
    {
        foreach (var prs in processesToKill)
        {
            try
            {
                prs.Kill();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Impossibile terminare il processo {prs.ProcessName}. Errore: {ex.Message}");
            }
        }
    }

    #endregion

    #region SSH e MySQL

    /// <summary>
    /// Avvia il tunnel SSH su 'boundPort' (di solito 3306) con le credenziali
    /// definite nella configurazione. Ritorna true se la connessione SSH + tunnel
    /// è riuscita, false in caso di problemi o credenziali assenti.
    /// </summary>
    /// <remarks>
    /// Se la connessione ha successo, la property MySql.Host viene impostata a "localhost",
    /// e la connessione MySQL locale punta al tunnel.
    /// </remarks>
    public static bool EnableSshIfPossible(uint boundPort)
    {
        var cred = GenericMySQLConfigurationNew.Instance.SelectedWebsite.SshCredentials;
        if (cred == null || string.IsNullOrEmpty(cred.Username) || string.IsNullOrEmpty(cred.Password))
            return false; // SSH non configurato

        // Creiamo il client SSH (SelectedWebsiteSsh)
        _sshClient = GenericMySQLConfigurationNew.Instance.SelectedWebsiteSsh;
        try
        {
            _sshClient.Connect();
            if (!_sshClient.IsConnected)
                return false;
            Console.WriteLine($"SSH Connected: {_sshClient.IsConnected}");

            // Creiamo e avviamo il forward di porta
            var portForward = new ForwardedPortLocal("127.0.0.1", boundPort, "localhost", boundPort);
            try
            {
                _sshClient.AddForwardedPort(portForward);
                portForward.Start();
                Console.WriteLine("Port forwarding started on 127.0.0.1:3306");

                Console.WriteLine(portForward.IsStarted
                    ? $"Port forwarding attivo su {portForward.BoundHost}:{portForward.BoundPort}"
                    : "Il port forwarding non è stato avviato correttamente.");
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Errore nell'attivare il tunnel SSH: {ex.Message}");
                _sshClient.Disconnect();
                return false;
            }

            // Imposto l'host su "localhost" per MySQL, perché ora usiamo il tunnel
            GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.Host = "localhost";

            // Test di apertura rapida (short timeout)
            var testConn = GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.ShortTimeout;
            testConn.Open();
            testConn.Close();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Errore nella connessione SSH: {ex.Message}");
            _sshClient?.Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Verifica se la connessione al DB è raggiungibile (aprendo e chiudendo velocemente la connessione).
    /// Se non viene specificato un MySqlConnection, usa il ShortTimeout predefinito.
    /// </summary>
    public static bool CheckDbConnection(MySqlConnection mySqlConnection = null)
    {
        try
        {
            mySqlConnection ??= GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.ShortTimeout;
            mySqlConnection.Open();
            mySqlConnection.Close();
            return true;
        }
        catch
        {
            // Log dell'eccezione, se necessario
            return false;
        }
    }

    #endregion
}