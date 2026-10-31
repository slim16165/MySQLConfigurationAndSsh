using System.Threading.Tasks;
using SerpRankingAPI.Config;

namespace MySQLConfigurationAndSsh;

/// <summary>Legacy caller adapter; no modal dialogs or process termination.</summary>
internal static class UIHelper
{
    internal static async Task<bool> UIConnectToDatabaseSsh(uint boundPort = 3306)
    {
        var site = WebSiteConfiguration.SelectedWebsite;
        var tunnelNeeded = ConnectionHelper.RequiresTunnel(site);
        var started = !tunnelNeeded || await Task.Run(
            () => ConnectionHelper.EnableSshIfPossible(site)).ConfigureAwait(false);
        return started && await ConnectionHelper.CheckDbConnectionAsync().ConfigureAwait(false);
    }
}
