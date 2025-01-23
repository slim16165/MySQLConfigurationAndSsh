using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MySQLConfigurationAndSsh;

/// <summary>
/// Classe statica che esegue il comando netstat e restituisce l'elenco delle porte usate e dal quale processo.
/// </summary>
public static class ProcessPorts
{
    private const int DEFAULT_NETSTAT_TIMEOUT_MS = 2000;   // Timeout di 2 secondi per netstat
    private const int DEFAULT_PROCESSNAME_TIMEOUT_MS = 100; // Timeout breve per la lettura del nome processo

    /// <summary>
    /// Restituisce la mappatura (ProcessPort) chiamando <see cref="GetNetStatPortsAsync"/>.
    /// </summary>
    public static List<ProcessPort> ProcessPortMap
        => Task.Run(async () => await GetNetStatPortsAsync(DEFAULT_NETSTAT_TIMEOUT_MS)).GetAwaiter().GetResult();

    
    /// <summary>
    /// Esegue il comando <c>netstat.exe -a -n -o</c>, lo analizza, e restituisce la lista di <see cref="ProcessPort"/>.
    /// La chiamata a <c>netstat</c> ha un timeout di <paramref name="netstatTimeoutMs"/> ms.
    /// </summary>
    /// <param name="netstatTimeoutMs">Timeout per il comando netstat (ms)</param>
    public static Task<List<ProcessPort>> GetNetStatPortsAsync(int netstatTimeoutMs = DEFAULT_NETSTAT_TIMEOUT_MS)
    {
        return Task.Run(async () =>
        {
            try
            {
                // Avvio netstat in modo asincrono con un timeout
                string netstatOutput = await RunNetStatCommandAsync(netstatTimeoutMs).ConfigureAwait(false);

                // Parsing dell'output
                List<ProcessPort> netStatPorts = ParseNetStatOutput(netstatOutput);
                return netStatPorts;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetNetStatPortsAsync] Errore: {ex.Message}");
                return new List<ProcessPort>();
            }
        });
    }


    #region Invocazione netstat con timeout

    /// <summary>
    /// Avvia <c>netstat.exe -a -n -o</c> e ritorna l'output come stringa, 
    /// bloccando l'operazione dopo <paramref name="timeoutMs"/> ms.
    /// </summary>
    private static async Task<string> RunNetStatCommandAsync(int timeoutMs)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-a -n -o",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Gestione dell'output in modalità asincrona
        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null) outputBuilder.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null) errorBuilder.AppendLine(args.Data);
        };

        // Avvio del processo
        if (!process.Start())
        {
            throw new InvalidOperationException("Impossibile avviare netstat.exe");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Utilizziamo Task.Run + WaitForExit con un timeout
        bool exitedInTime = await Task.Run(() => process.WaitForExit(timeoutMs)).ConfigureAwait(false);
        if (!exitedInTime)
        {
            try
            {
                process.Kill(); // in caso di blocco, kill del processo
            }
            catch
            {
                // Ignoro eventuali eccezioni di kill
            }
            throw new TimeoutException($"netstat non ha risposto entro {timeoutMs} ms.");
        }

        // Eseguito correttamente, controllo exitCode
        if (process.ExitCode != 0)
        {
            Debug.WriteLine("NetStat command exited with non-zero code. Potrebbero servire permessi elevati.");
            Debug.WriteLine(errorBuilder.ToString());
        }

        return outputBuilder.ToString();
    }

    #endregion

    #region Parsing output netstat

    /// <summary>
    /// Parsea la stringa di output di netstat e costruisce la lista di <see cref="ProcessPort"/>.
    /// </summary>
    private static List<ProcessPort> ParseNetStatOutput(string output)
    {
        return Regex.Split(output, "\r\n")
            .Where(IsRelevantRow)
            .Select(ParseRow)
            .Where(pp => pp != null)
            .ToList();
    }

    /// <summary>
    /// Determina se una determinata riga è rilevante (non vuota e non un'intestazione).
    /// </summary>
    private static bool IsRelevantRow(string row)
    {
        if (string.IsNullOrWhiteSpace(row)) return false;
        row = row.Trim();
        return !(row.StartsWith("Proto", StringComparison.OrdinalIgnoreCase) ||
                 row.StartsWith("Active", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Parsea una singola riga di netstat in un oggetto <see cref="ProcessPort"/>.
    /// </summary>
    private static ProcessPort ParseRow(string row)
    {
        try
        {
            string[] tokens = Regex.Split(row.Trim(), "\\s+");
            if (tokens.Length > 4 && (tokens[0].Equals("TCP", StringComparison.OrdinalIgnoreCase) ||
                                      tokens[0].Equals("UDP", StringComparison.OrdinalIgnoreCase)))
            {
                return ParseProcessPort(tokens);
            }

            Debug.WriteLine($"[ParseRow] Riga netstat non riconosciuta: {row}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ParseRow] Errore parse: {ex.Message}. Riga: {row}");
            return null;
        }
    }

    /// <summary>
    /// Converte i token di una riga netstat in un oggetto <see cref="ProcessPort"/>.
    /// </summary>
    private static ProcessPort ParseProcessPort(string[] tokens)
    {
        try
        {
            string protocol = tokens[0];   // es: "TCP" o "UDP"
            // tokens[1] -> local address, tokens[2] -> remote address, tokens[3] -> state (TCP), tokens[4] -> PID
            // in alcuni formati netstat, la colonna di "state" potrebbe mancare per UDP, 
            // quindi potremmo dover adattare l'indice del PID
            int pidIndex = (protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase)) ? 3 : 4;

            if (!int.TryParse(tokens[pidIndex], out int processId))
            {
                // Log e fallback
                Debug.WriteLine("Impossibile leggere il PID dalla colonna attesa. Riga: " + string.Join(" ", tokens));

                return null;
            }

            // Ricaviamo l'indirizzo e la porta dalla colonna "local address" (UDP) o "local address" (TCP)
            // Netstat solitamente mette l'indirizzo in tokens[1] per TCP, 
            // mentre per UDP potresti trovarlo in tokens[1] o tokens[2] a seconda della local/remote
            // -> Adattiamo: assumiamo "local address" stia in tokens[1]
            string addressPort = tokens[1];
            // Sostituisce eventuali IPv6 con [1.1.1.1] come dummy se presenti
            string sanitized = Regex.Replace(addressPort, @"\[(.*?)\]", "1.1.1.1");
            string[] parts = sanitized.Split(':');
            if (parts.Length < 2)
            {
                Debug.WriteLine("Impossibile separare indirizzo da porta in: " + addressPort);
                return null;
            }

            // Porta
            if (!int.TryParse(parts[parts.Length - 1], out int portNumber))
            {
                Debug.WriteLine("Impossibile convertire la porta in intero: " + parts[parts.Length - 1]);
                return null;
            }

            // Nome del processo con un timeout breve
            string processName = GetProcessName(processId, DEFAULT_PROCESSNAME_TIMEOUT_MS);

            // Controlla se era IPv6
            bool isIPv6 = sanitized.Contains("1.1.1.1");
            string fullProtocol = isIPv6 ? $"{protocol}v6" : $"{protocol}v4";

            return new ProcessPort(processName, processId, fullProtocol, portNumber);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ParseProcessPort] Errore: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region GetProcessName con Timeout

    /// <summary>
    /// Restituisce il nome del processo dal processId, 
    /// forzando un timeout di <paramref name="timeoutMs"/> ms.
    /// Se scade il tempo o si verifica un errore, restituisce "UNKNOWN".
    /// </summary>
    private static string GetProcessName(int processId, int timeoutMs)
    {
        if (processId <= 0) return "UNKNOWN";

        using var cts = new CancellationTokenSource(timeoutMs);

        var task = Task.Run(() =>
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                return proc.ProcessName ?? "UNKNOWN";
            }
            catch
            {
                // Se process non esiste più o accesso negato
                return "UNKNOWN";
            }
        }, cts.Token);

        try
        {
            if (task.Wait(timeoutMs))
            {
                return task.Result;
            }
            else
            {
                Debug.WriteLine($"[GetProcessName] Timeout nel recuperare il nome per pid {processId}");
                return "UNKNOWN";
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[GetProcessName] Cancellazione o scadenza del token per pid {processId}");
            return "UNKNOWN";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetProcessName] Errore generico pid {processId}: {ex.Message}");
            return "UNKNOWN";
        }
    }

    #endregion
}