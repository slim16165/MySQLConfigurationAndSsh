using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using MySQLConfigurationAndSsh.Config;
using MySQLConfigurationAndSsh.ProcessPorts;
using MySqlConnector;
using Renci.SshNet;

namespace MySQLConfigurationAndSsh
{
    public class ConnectionHelper
    {
        public static SshClient sshClient;

        public static string GetIPAddress()
        {
            String address = "";
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                address = stream.ReadToEnd();
            }

            int first = address.IndexOf("Address: ") + 9;
            int last = address.LastIndexOf("</body>");
            address = address.Substring(first, last - first);

            return address;
        }

        public static IPAddress GetLocalIPv4AddressRequiresInternet()
        {
            var localIp = IPAddress.None;
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    // Connect socket to Google's Public DNS service
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is not IPEndPoint endPoint)
                    {
                        throw new InvalidOperationException($"Error occurred casting {socket.LocalEndPoint} to IPEndPoint");
                    }
                    localIp = endPoint.Address;
                }
            }
            catch (SocketException ex)
            {
                // Handle exception
            }

            return localIp;
        }

        public static event EventHandler<TupleEventArgs<string, List<Process>>> ShowMessageBoxEvent;

        public static void CheckAndHandlePortIsUsed()
        {
            //Potrei essermi già connesso, in tal caso non devo fare alcun controllo
            if(sshClient is { IsConnected: true })
                return;

            //potrei avere uno scambio di messaggi asincroni, un riferimento al chiamante, esporre degli eventi 
            //Deconstructor
            var (processesUsingPort, debug_message) = CheckIfPortIsUsed(3306);

            if (processesUsingPort.Any())
            {
                string text = $"Un altro programma sta utilizzando la porta 3306, vuoi terminarlo?\n{debug_message}";
                
                //var k = new List<(string Testo, bool info)>();
                ShowMessageBoxEvent?.Invoke(typeof(ConnectionHelper), TupleEventArgs.Create(text, processesUsingPort));
            }
        }

        //protected virtual void OnRaiseShowMessageBoxEvent(EventArgs e)
        //{
        //    // Call to raise the event.
        //    ShowMessageBoxEvent?.Invoke(this, e);
        //}

        public static void KillProcess(List<Process> processToKill)
        {
            foreach (var prs in processToKill)
            {
                prs.Kill();
            }
        }

        public static bool EnableSshIfPossible(uint boundPort)
        {
            
            try
            {
                //If we don't have SSH credentials, use normal DB connection
                var cred = GenericMySQLConfigurationNew.Instance.SelectedWebsite.SshCredentials;
                if (cred == null || string.IsNullOrEmpty(cred.Username) || string.IsNullOrEmpty(cred.Password))
                    return false;

                sshClient = GenericMySQLConfigurationNew.Instance.SelectedWebsiteSsh;
                sshClient.Connect();

                if (!sshClient.IsConnected) return false;

                var port = new ForwardedPortLocal("localhost", boundPort, "localhost", boundPort);
                
                try
                {
                    sshClient.AddForwardedPort(port);
                    port.Start();
                }
                catch (SocketException ex)
                {
                    // Gestisci l'eccezione qui, ad esempio stampando un messaggio di errore e terminando l'applicazione.
                    Console.WriteLine("Errore: " + ex.Message);
                    sshClient.Disconnect();
                        
                }

                GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.Host = "localhost";


                GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.ShortTimeout.Open();

                return true;

                return false;
            }
            catch (Exception e)
            {

                throw;
            }
        }

        public static (List<Process>, string) CheckIfPortIsUsed(uint boundPort)
        {
            List<ProcessPort> processes = ProcessPorts.ProcessPorts.ProcessPortMap.FindAll(x => x.PortNumber == boundPort);

            if (!processes.Any()) return (new List<Process>(), "");

            var message = processes.Select(s => s.ProcessPortDescription).Aggregate((a, b) => a + "\n" + b);
            var processesUsingPort = from proc in Process.GetProcesses()
                join procPort in processes on proc.Id equals procPort.ProcessId
                select proc;

            return (processesUsingPort.ToList(), message);

        }

        public static bool CheckDbConnection(MySqlConnection mySqlConnection = null)
        {
            bool result;
            try
            {
                if(mySqlConnection == null)
                    mySqlConnection = GenericMySQLConfigurationNew.Instance.SelectedWebsite.MySql.ShortTimeout;
                mySqlConnection.Open();
                result = true;
                mySqlConnection.Close();

                //client.Disconnect();
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }
    }

    public static class TupleEventArgs
    {
        static public TupleEventArgs<T1> Create<T1>(T1 item1)
        {
            return new TupleEventArgs<T1>(item1);
        }

        static public TupleEventArgs<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new TupleEventArgs<T1, T2>(item1, item2);
        }

        static public TupleEventArgs<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
        {
            return new TupleEventArgs<T1, T2, T3>(item1, item2, item3);
        }
    }

    public class TupleEventArgs<T1> : EventArgs
    {
        public T1 Item1;

        public TupleEventArgs(T1 item1)
        {
            Item1 = item1;
        }
    }

    public class TupleEventArgs<T1, T2> : EventArgs
    {
        public T1 Item1;
        public T2 Item2;

        public TupleEventArgs(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
    }

    public class TupleEventArgs<T1, T2, T3> : EventArgs
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;

        public TupleEventArgs(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }
    }
}
