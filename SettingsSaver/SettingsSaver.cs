using System.Configuration;
using System.IO.IsolatedStorage;
using System.IO;

namespace MySQLConfigurationAndSsh
{
    internal static class SettingsSaver
    {
        public static void SaveOnConfigFile(string settingkey, string? settingvalue)
        {
            // scrivere un'impostazione
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[settingkey] != null)
            {
                // Se l'impostazione esiste già, aggiorna il suo valore
                config.AppSettings.Settings[settingkey].Value = settingvalue;
            }
            else
            {
                // Altrimenti, aggiungi una nuova impostazione
                config.AppSettings.Settings.Add(settingkey, settingvalue);
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
}
