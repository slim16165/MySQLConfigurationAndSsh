using System.Collections.Generic;
using System.Xml.Serialization;
using MySQLConfigurationAndSsh.Config;

namespace MySQLConfigurationAndSsh.Config.Data;

[XmlRoot("RootConfig")]
public class RootConfigBase
{
    [XmlElement("WebsiteAppConfig")]
    public List<WebsiteAppConfigBase> Siti { get; set; }
}
