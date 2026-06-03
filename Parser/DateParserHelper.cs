using System.Text.RegularExpressions;

namespace MySQLConfigurationAndSsh.Parser;

internal class DateParserHelper
{
    public static string EstraiData(string testo)
    {
        // Regex per riconoscere date in vari formati, inclusi quelli con mesi scritti in lettere
        string pattern =
            @"\b\d{1,2}[- /.](?:\d{1,2}|gennaio|febbraio|marzo|aprile|maggio|giugno|luglio|agosto|settembre|ottobre|novembre|dicembre)[- /.]\d{2,4}\b";

        // Uso di Regex.Match per trovare la data nel testo
        Match match = Regex.Match(testo, pattern);

        return match.Success ? match.Value : null;
    }
}
