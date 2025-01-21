using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MySQLConfigurationAndSsh.ProcessPorts;

/// <summary>
/// Static class that returns the list of processes and the ports those processes use.
/// </summary>
public static class ProcessPorts
{
    /// <summary>
    /// A list of ProcesesPorts that contain the mapping of processes and the ports that the process uses.
    /// </summary>
    public static List<ProcessPort> ProcessPortMap => GetNetStatPorts();


    /// <summary>
    /// This method distills the output from netstat -a -n -o into a list of ProcessPorts that provide a mapping between
    /// the process (name and id) and the ports that the process is using.
    /// </summary>
    /// <returns></returns>
    public static List<ProcessPort> GetNetStatPorts()
    {
        try
        {
            string netStatOutput = RunNetStatCommand();
            List<ProcessPort> netStatPorts = ParseNetStatOutput(netStatOutput);
            return netStatPorts;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetNetStatPorts: {ex.Message}");
            return new List<ProcessPort>();
        }
    }

    private static string RunNetStatCommand()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-a -n -o",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Console.WriteLine("NetStat command failed. This may require elevated permissions.");
            Console.WriteLine(error);
        }

        return output;
    }

    private static List<ProcessPort> ParseNetStatOutput(string output)
    {
        return Regex.Split(output, "\r\n")
            .Where(IsRelevantRow)
            .Select(ParseRow)
            .Where(processPort => processPort != null)
            .ToList();
    }

    private static bool IsRelevantRow(string row)
    {
        return !string.IsNullOrWhiteSpace(row) &&
               !row.Trim().StartsWith("Proto") &&
               !row.Trim().StartsWith("Active");
    }

    private static ProcessPort ParseRow(string row)
    {
        try
        {
            string[] tokens = Regex.Split(row.Trim(), "\\s+");
            if (tokens.Length > 4 && (tokens[0] == "UDP" || tokens[0] == "TCP"))
            {
                return ParseProcessPort(tokens);
            }

            Debug.WriteLine($"Unrecognized NetStat row: {row}");
            return null;
        }
        catch
        {
            Debug.WriteLine($"Failed to parse NetStat row: {row}");
            return null;
        }
    }

    private static ProcessPort ParseProcessPort(string[] tokens)
    {
        string protocol = tokens[1];
        string address = Regex.Replace(tokens[2], @"\[(.*?)\]", "1.1.1.1");
        bool isIPv6 = address.Contains("1.1.1.1");
        int portNumber = Convert.ToInt32(address.Split(':')[1]);
        int processId = Convert.ToInt32(tokens[4]);
        var processName = GetProcessName(processId);

        string fullProtocol = isIPv6 ? $"{protocol}v6" : $"{protocol}v4";

        return new ProcessPort(processName, processId, fullProtocol, portNumber);
    }

    /// <summary>
    /// Private method that handles pulling the process name (if one exists) from the process id.
    /// </summary>
    /// <param name="processId"></param>
    /// <returns></returns>
    private static string GetProcessName(int processId)
    {
        string procName = "UNKNOWN";

        try
        {
            procName = Process.GetProcessById(processId).ProcessName;
        }
        catch { }

        return procName;
    }
}