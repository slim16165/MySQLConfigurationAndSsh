using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MySQLConfigurationAndSsh.ProcessPorts
{
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
        private static List<ProcessPort> GetNetStatPorts()
        {
            List<ProcessPort> processPorts = new List<ProcessPort>();

            try
            {
                using (Process proc = new Process())
                {

                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "netstat.exe";
                    startInfo.Arguments = "-a -n -o";
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardInput = true;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;

                    proc.StartInfo = startInfo;
                    proc.Start();

                    StreamReader standardOutput = proc.StandardOutput;
                    StreamReader standardError = proc.StandardError;

                    string netStatContent = standardOutput.ReadToEnd() + standardError.ReadToEnd();
                    string netStatExitStatus = proc.ExitCode.ToString();

                    if (netStatExitStatus != "0")
                    {
                        Console.WriteLine("NetStat command failed.   This may require elevated permissions.");
                    }

                    string[] netStatRows = Regex.Split(netStatContent, "\r\n");

                    foreach (string netStatRow in netStatRows)
                    {
                        string[] tokens = Regex.Split(netStatRow.Trim(), "\\s+");
                        if (tokens.Length > 4 && (tokens[1].Equals("UDP") || tokens[1].Equals("TCP")))
                        {
                            string ipAddress = Regex.Replace(tokens[2], @"\[(.*?)\]", "1.1.1.1");
                            try
                            {
                                int processId = Convert.ToInt16(tokens[4]);
                                int int16 = Convert.ToInt16(tokens[5]);
                                string processName = tokens[1] == "UDP" ? GetProcessName(processId) : GetProcessName(int16);
                                int processId2 = tokens[1] == "UDP" ? processId : int16;
                                string protocol = ipAddress.Contains("1.1.1.1") ? $"{tokens[1]}v6" : $"{tokens[1]}v4";
                                int portNumber = Convert.ToInt32(ipAddress.Split(':')[1]);

                                var processPort = new ProcessPort(
                                    processName,
                                    processId2,
                                    protocol,
                                    portNumber
                                );

                                processPorts.Add(processPort);
                            }
                            catch
                            {
                                Console.WriteLine("Could not convert the following NetStat row to a Process to Port mapping.");
                                Console.WriteLine(netStatRow);
                            }
                        }
                        else
                        {
                            if (!netStatRow.Trim().StartsWith("Proto") && !netStatRow.Trim().StartsWith("Active") && !string.IsNullOrWhiteSpace(netStatRow))
                            {
                                Console.WriteLine("Unrecognized NetStat row to a Process to Port mapping.");
                                Console.WriteLine(netStatRow);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return processPorts;
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
}