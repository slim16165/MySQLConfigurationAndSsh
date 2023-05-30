using MySQLConfigurationAndSsh.Config.Data;

namespace MySQLConfigurationAndSsh.Config;

public class WebsiteAppConfigBase
{
    /// <summary>
    /// Imposta quale sito debba essere autoload
    /// </summary>
    private static string? AutoloadSite
    {
        get => SettingsSaver.LoadFromConfigFile("AutoloadSite");
        set => SettingsSaver.SaveOnConfigFile("AutoloadSite", value);
    }

    public Data.MySqlConnectionAppConfig MySql { get; set; }
    public SshCredentials SshCredentials { get; set; }
    public string? ShortSiteName { get; set; }

    public bool IsAutoload
    {
        get => ShortSiteName == WebsiteAppConfigBase.AutoloadSite;
        set => WebsiteAppConfigBase.AutoloadSite = ShortSiteName;
    }
}