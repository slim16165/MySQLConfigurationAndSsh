using MySqlConnector;

namespace MySQLConfigurationAndSsh.Config.Data;

public class MySqlConnectionAppConfig
{
    public string Host { get; set; }

    public int Port { get; set; }

    public string UserId { get; set; }

    public string Database { get; set; }

    public string Password { get; set; }

    private string BaseConnectionString => $"server={Host};database={Database};user={UserId};password={Password};Compress=true;Convert Zero Datetime=true;AllowZeroDateTime=true";

    public string ConnectionString => BaseConnectionString;

    public MySqlConnection Connection => new MySqlConnection(BaseConnectionString);

    public MySqlConnection ShortTimeoutConnection => new MySqlConnection(BaseConnectionString + ";ConnectionTimeout=5");


    //public static implicit operator MySqlConnection(MySqlConnectionAppConfig o)
    //{
    //    return new MySqlConnection
    //    {
    //        ConnectionString =
    //            $"server={o.Host};database={o.Database};user={o.UserId};password={o.Password};Compress=true;Convert Zero Datetime=true;AllowZeroDateTime=true"
    //    };
    //}
}