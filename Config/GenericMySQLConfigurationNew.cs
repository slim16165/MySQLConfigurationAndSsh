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
    public abstract class GenericMySQLConfigurationNew
    {
        public List<WebsiteAppConfigBase> WebsitesConfigs { get; set; }

        //Vedi classe WebSiteConfiguration
        public virtual WebsiteAppConfigBase SelectedWebsite
        {
            get
            {
                if (!string.IsNullOrEmpty(SelectedWebsiteName))
                {
                    if(WebsitesConfigs == null)
                        throw new Exception("Lista siti non inizializzata, controlla la classe che eredita da questa");

                    return WebsitesConfigs.Single(w => w.ShortSiteName == SelectedWebsiteName);
                }
                else 
                    throw new Exception("Non è stato selezionato alcun sito");
            }
        }

        public virtual SshClient SelectedWebsiteSsh
        {
            get
            {
                SshCredentials cred = SelectedWebsite.SshCredentials;
                SshClient selectedWebsiteSsh = new SshClient(SelectedWebsite.MySql.Host, 22, cred.Username, cred.Password);
                return selectedWebsiteSsh;
            }
        }

        public virtual string? SelectedWebsiteName { get; set; }


        public virtual void SaveConfig()
        {
            RootConfigBase rootConfig = new RootConfigBase() { Siti = WebsitesConfigs };

            var x1 = new XmlSerializer(typeof(RootConfigBase));
            using (var fs1 = new FileStream(@"Sites.config", FileMode.OpenOrCreate))
            {
                x1.Serialize(fs1, rootConfig);
            }
        }

        public virtual void LoadConfig()
        {
            var x2 = new XmlSerializer(typeof(RootConfigBase));
            using (var fs2 = new FileStream(@"Sites.config", FileMode.Open))
            {
                RootConfigBase rootConfig = (RootConfigBase)x2.Deserialize(fs2);
                Instance.WebsitesConfigs = rootConfig.Siti;
            }

            if (WebsitesConfigs.Count == 1)
                SelectedWebsiteName = WebsitesConfigs[0].ShortSiteName;
        }

        public static GenericMySQLConfigurationNew Instance { get; set; }

        public abstract WebsiteAppConfigBase GetSelectedWebsite();
    }
}