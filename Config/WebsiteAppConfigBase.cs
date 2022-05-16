using MySQLConfigurationAndSsh.Config.Data;

namespace MySQLConfigurationAndSsh.Config
{
    public class WebsiteAppConfigBase
    {
        public Data.MySqlConnectionAppConfig MySql { get; set; }
        public SshCredentials SshCredentials { get; set; }
        public string? ShortSiteName { get; set; }
    }
}