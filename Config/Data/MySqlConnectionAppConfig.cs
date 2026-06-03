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

    public MySqlConnection Connection => new MySqlConnection(BaseConnectionString);

    public MySqlConnection ShortTimeoutConnection => new MySqlConnection(BuildConnectionString(timeoutSeconds: 5));

    public MySqlConnection ShortTimeout => ShortTimeoutConnection;

    public static implicit operator MySqlConnection(MySqlConnectionAppConfig config)
    {
        return config.Connection;
    }

    private string BuildConnectionString(int? timeoutSeconds = null)
    {
        var portSegment = Port > 0 ? $";port={Port}" : string.Empty;
        var timeoutSegment = timeoutSeconds.HasValue ? $";ConnectionTimeout={timeoutSeconds.Value}" : string.Empty;

        return $"server={Host}{portSegment};database={Database};user={UserId};password={Password};Compress=true;Convert Zero Datetime=true;AllowZeroDateTime=true{timeoutSegment}";
    }
}
