using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MySQLConfigurationAndSsh;

internal static class UIHelper
{
    internal static async void UIConnectToDatabaseSsh(uint boundPort = 3306)
    {
        try
        {
            CheckAndHandlePortIsUsed();

            ConnectionHelper.EnableSshIfPossible(boundPort);
            await ConnectionHelper.CheckDbConnectionAsync().ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            MessageBox.Show(ex.Message + Environment.NewLine +
                            "È possibile che un altro programma stia utilizzando la porta 3306, proveremo la connessione senza SSH",
                "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void CheckAndHandlePortIsUsed()
    {
        void OnConnectionHelperOnShowMessageBoxEvent(object sender, TupleEventArgs<string, List<Process>> tupleEventArgs)
        {
            var result = MessageBox.Show(tupleEventArgs.Item1, "Errore", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                ConnectionHelper.KillProcess(tupleEventArgs.Item2);
            }
        }

        ConnectionHelper.ShowMessageBoxEvent += OnConnectionHelperOnShowMessageBoxEvent;
        ConnectionHelper.CheckAndHandlePortIsUsed();
    }
}
