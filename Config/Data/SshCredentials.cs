namespace MySQLConfigurationAndSsh.Config.Data;

public class SshCredentials
{
    public string Username { get; set; }

    public string Password { get; set; }

    public int? Port { get; set; }

    /// <summary>Configured OpenSSH host alias; relies on the user's SSH key/agent.</summary>
    public string OpenSshAlias { get; set; }
}
