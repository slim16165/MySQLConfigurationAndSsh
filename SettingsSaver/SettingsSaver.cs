using System.Configuration;
using System.IO;
using System.IO.IsolatedStorage;

namespace MySQLConfigurationAndSsh;

internal static class SettingsSaver
{
    public static void SaveOnConfigFile(string settingKey, string? settingValue)
    {
        Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        if (config.AppSettings.Settings[settingKey] != null)
        {
            config.AppSettings.Settings[settingKey].Value = settingValue;
        }
        else
        {
            config.AppSettings.Settings.Add(settingKey, settingValue);
        }

        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    public static string? LoadFromConfigFile(string settingKey)
    {
        return ConfigurationManager.AppSettings[settingKey];
    }

    public static void SaveOnIsolatedStorage()
    {
        using IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForAssembly();
        using IsolatedStorageFileStream stream = new IsolatedStorageFileStream("settings.xml", FileMode.OpenOrCreate, storage);
        using StreamWriter writer = new StreamWriter(stream);
        writer.Write("SettingValue");
    }
}
