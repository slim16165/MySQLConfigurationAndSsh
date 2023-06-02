using System.Collections.Generic;
using System.Xml.Serialization;

namespace MySQLConfigurationAndSsh.Config.Data;

[XmlRoot("RootConfig")]
public class RootConfigBase
{
    [XmlElement("WebsiteAppConfig")]
    public List<WebsiteAppConfigBase> Siti { get; set; }
}