using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MySQLConfigurationAndSsh.Config.Data;
using Renci.SshNet;

namespace MySQLConfigurationAndSsh.Config
{
    //Vedi classe WebSiteConfiguration
    public abstract class GenericMySQLConfiguration
    {
        public static List<WebsiteAppConfigBase> WebsitesConfigsBase { get; set; }

        //Vedi classe WebSiteConfiguration
        public static WebsiteAppConfigBase SelectedWebsite
        {
            get
            {
                if (!string.IsNullOrEmpty(SelectedWebsiteName))
                {
                    if(WebsitesConfigsBase == null)
                        throw new Exception("Lista siti non inizializzata, controlla la classe che eredita da questa");

                    return WebsitesConfigsBase.Single(w => w.ShortSiteName == SelectedWebsiteName);
                }
                else 
                    throw new Exception("Non è stato selezionato alcun sito");
            }
        }

        public static SshClient SelectedWebsiteSsh
        {
            get
            {
                SshCredentials cred = SelectedWebsite.SshCredentials;
                SshClient selectedWebsiteSsh = new SshClient(SelectedWebsite.MySql.Host, 22, cred.Username, cred.Password);
                return selectedWebsiteSsh;
            }
        }

        public static string? SelectedWebsiteName { get; set; }


        public static void SaveConfig()
        {
            RootConfigBase rootConfig = new RootConfigBase() { Siti = WebsitesConfigsBase };

            var x1 = new XmlSerializer(typeof(RootConfigBase));
            using (var fs1 = new FileStream(@"Sites.config", FileMode.OpenOrCreate))
            {
                x1.Serialize(fs1, rootConfig);
            }
        }

        public static void LoadConfig()
        {
            var x2 = new XmlSerializer(typeof(RootConfigBase));
            using (var fs2 = new FileStream(@"Sites.config", FileMode.Open))
            {
                RootConfigBase rootConfig = (RootConfigBase)x2.Deserialize(fs2);
                GenericMySQLConfiguration.WebsitesConfigsBase = rootConfig.Siti;
            }

            if (WebsitesConfigsBase.Count == 1)
                SelectedWebsiteName = WebsitesConfigsBase[0].ShortSiteName;
        }

        public abstract WebsiteAppConfigBase GetSelectedWebsite();
    }
}