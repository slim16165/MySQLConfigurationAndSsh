using MySQLConfigurationAndSsh.Config.Data;

namespace MySQLConfigurationAndSsh.Config;

public class WebsiteAppConfigBase
{
    private static string? AutoloadSite
    {
        get => SettingsSaver.LoadFromConfigFile("AutoloadSite");
        set => SettingsSaver.SaveOnConfigFile("AutoloadSite", value);
    }

    public MySqlConnectionAppConfig MySql { get; set; }

    public SshCredentials SshCredentials { get; set; }

    public string? ShortSiteName { get; set; }

    public bool IsAutoload
    {
        get => ShortSiteName == AutoloadSite;
        set => AutoloadSite = value ? ShortSiteName : null;
    }
}
