using System;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Renci.SshNet;

namespace MySQLConfigurationAndSsh;

public static class SshEstensionMethod
{
    private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public static void MyDispose(this SshClient _client)
    {
        if (_client == null) return;

        Task.Run(() =>
        {
            _log.Debug("Disposing _client");

            var timer = new System.Timers.Timer();

            timer.Interval = 2000;
            timer.AutoReset = false;

            timer.Elapsed += (s, e) =>
            {
                try
                {
                    var sessionField = _client.GetType()
                        .GetProperty("Session", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (sessionField != null)
                    {
                        var session = sessionField.GetValue(_client);

                        if (session != null)
                        {
                            var socketField = session.GetType().GetField("_socket",
                                BindingFlags.NonPublic | BindingFlags.Instance);

                            if (socketField != null)
                            {
                                var socket = (Socket)socketField.GetValue(session);

                                if (socket != null)
                                {
                                    _log.Debug(
                                        $"Socket state: Connected = {socket.Connected}, Blocking = {socket.Blocking}, Available = {socket.Available}, LocalEndPoint = {socket.LocalEndPoint}, RemoteEndPoint = {socket.RemoteEndPoint}");

                                    _log.Debug("Set _socket to null");

                                    try
                                    {
                                        socket.Dispose();
                                    }
                                    catch (Exception ex)
                                    {
                                        _log.Debug("Exception disposing _socket", ex);
                                    }

                                    socketField.SetValue(session, null);
                                }
                                else
                                {
                                    _log.Debug("_socket was null");
                                }
                            }

                            var messageListenerCompletedField = session.GetType()
                                .GetField("_messageListenerCompleted",
                                    BindingFlags.NonPublic | BindingFlags.Instance);

                            var messageListenerCompleted =
                                (EventWaitHandle)messageListenerCompletedField.GetValue(session);

                            if (messageListenerCompleted != null)
                            {
                                var waitHandleSet = messageListenerCompleted.WaitOne(0);

                                _log.Debug($"_messageListenerCompleted was set = {waitHandleSet}");

                                if (!waitHandleSet)
                                {
                                    _log.Debug($"Calling Set()");
                                    messageListenerCompleted.Set();
                                }
                            }
                            else
                            {
                                _log.Debug("_messageListenerCompleted was null");
                            }
                        }
                        else
                        {
                            _log.Debug("Session was null");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Debug($"Exception in Timer event handler", ex);
                }
            };

            timer.Start();

            _client.Dispose();

            _log.Info("Disposed _client");
        });
    }
}