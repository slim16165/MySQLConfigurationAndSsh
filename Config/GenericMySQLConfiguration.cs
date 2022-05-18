using System;
using System.Collections.Generic;
using System.Linq;
using MySQLConfigurationAndSsh.Config.Data;
using Renci.SshNet;

namespace MySQLConfigurationAndSsh.Config
{
    //Vedi classe WebSiteConfiguration
    public abstract class GenericMySQLConfiguration
    {
        public static List<WebsiteAppConfigBase> WebsitesConfigsBase { get; set; }

        public virtual WebsiteAppConfigBase GetSelectedWebsite()
        {
            if (!string.IsNullOrEmpty(SelectedWebsiteName))
            {
                if (WebsitesConfigsBase == null)
                    throw new Exception("Lista siti non inizializzata, controlla la classe che eredita da questa");

                return WebsitesConfigsBase.Single(w => w.ShortSiteName == SelectedWebsiteName);
            }
            else
                throw new Exception("Non è stato selezionato alcun sito");
        }

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


        //public abstract void SaveConfig();
        //public abstract void LoadConfig();

    }
}