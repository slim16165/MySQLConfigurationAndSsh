using MySqlConnector;

namespace MySQLConfigurationAndSsh.Config.Data;

public class MySqlConnectionAppConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string UserId { get; set; }
    public string Database { get; set; }
    public string Password { get; set; }

    public string BaseConnectionString => BuildConnectionString();
    public string ConnectionString => BaseConnectionString;
    public MySqlConnection Connection => CreateConnection();
    public MySqlConnection ShortTimeoutConnection => CreateConnection(timeoutSeconds: 5);
    public MySqlConnection ShortTimeout => ShortTimeoutConnection;

    public MySqlConnection CreateConnection(string host = null, int? port = null, int? timeoutSeconds = null)
        => new MySqlConnection(BuildConnectionString(host, port, timeoutSeconds));

    public static implicit operator MySqlConnection(MySqlConnectionAppConfig config)
        => config.Connection;

    private string BuildConnectionString(string host = null, int? port = null, int? timeoutSeconds = null)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host ?? Host,
            Port = (uint)(port ?? (Port > 0 ? Port : 3306)),
            UserID = UserId,
            Database = Database,
            Password = Password,
            ConnectionTimeout = (uint)(timeoutSeconds ?? 15),
            ConvertZeroDateTime = true,
            AllowZeroDateTime = true
        };
        builder["Compress"] = true;
        return builder.ConnectionString;
    }
}
