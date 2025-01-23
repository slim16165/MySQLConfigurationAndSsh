using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Windows;
using NLog;
using MessageBox = System.Windows.MessageBox;

namespace MySQLConfigurationAndSsh;

internal static class UIHelper
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    internal static void UIConnectToDatabaseSsh(uint boundPort = 3306)
    {
        try
        {
            logger.Info("SSH connesso correttamente!");
            CheckAndHandlePortIsUsed();

            //Se non ho le credenziali SSH non lo abilito lo stesso, ma devo comunque collegarmi
            ConnectionHelper.EnableSshIfPossible(boundPort);

            bool checkDbConnection = ConnectionHelper.CheckDbConnection();
        }
        catch (SocketException ex)
        {
            logger.Error(ex, "Errore apertura SSH");
            //AccessDenied    10013    Si è tentato di accedere a un oggetto Socket secondo modalità non consentite dalle relative autorizzazioni di accesso.
            MessageBox.Show(ex.Message + Environment.NewLine +
                            "È possibile che un altro programma stia utilizzando la porta 3306, proveremo la connessione senza SSH",
                "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Errore apertura SSH");
            //AccessDenied    10013    Si è tentato di accedere a un oggetto Socket secondo modalità non consentite dalle relative autorizzazioni di accesso.
            MessageBox.Show(ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CheckAndHandlePortIsUsed()
    {
        void OnConnectionHelperOnShowMessageBoxEvent(object sender, TupleEventArgs<string, List<Process>> tupleEventArgs)
        {
            var r = MessageBox.Show(tupleEventArgs.Item1, "Errore", MessageBoxButton.YesNo);

            if (r == MessageBoxResult.Yes)
            {
                var processesUsingPort = tupleEventArgs.Item2;
                ConnectionHelper.KillProcesses(processesUsingPort);
            }
        }

        ConnectionHelper.ShowMessageBoxEvent += OnConnectionHelperOnShowMessageBoxEvent;

        ConnectionHelper.CheckAndHandlePortIsUsed();
    }
}