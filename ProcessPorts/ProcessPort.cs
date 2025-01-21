namespace MySQLConfigurationAndSsh.ProcessPorts;

/// <summary>
/// A mapping for processes to ports and ports to processes that are being used in the system.
/// </summary>
public class ProcessPort
{
    private string _processName = string.Empty;
    private int _processId = 0;
    private string _protocol = string.Empty;
    private int _portNumber = 0;

    /// <summary>
    /// Internal constructor to initialize the mapping of process to port.
    /// </summary>
    /// <param name="processName">Name of process to be </param>
    /// <param name="processId"></param>
    /// <param name="protocol"></param>
    /// <param name="portNumber"></param>
    internal ProcessPort(string processName, int processId, string protocol, int portNumber)
    {
        _processName = processName;
        _processId = processId;
        _protocol = protocol;
        _portNumber = portNumber;
    }

    public string ProcessPortDescription => $"{_processName} ({_protocol} port {_portNumber} pid {_processId})";

    public string ProcessName => _processName;

    public int ProcessId => _processId;

    public string Protocol => _protocol;

    public int PortNumber => _portNumber;
}