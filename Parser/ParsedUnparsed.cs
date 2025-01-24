using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace MySQLConfigurationAndSsh.Parser;

/// <summary>
/// Rappresenta un valore letto come stringa (Unparsed)
/// e convertito in un tipo forte (Parsed).
/// Utilizzato per gestire conversioni, errori di parsing e valori speciali.
/// </summary>
public class ParsedUnparsed<T>
{
    /// <summary>
    /// Nome/etichetta della colonna di origine (può essere utile per log/diagnostica).
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Valore grezzo (stringa) letto dalla sorgente (es. Excel).
    /// </summary>
    public string Unparsed { get; }

    /// <summary>
    /// Valore convertito in T, se il parsing ha avuto successo (IsParsed = true).
    /// </summary>
    public T Parsed { get; set; }

    /// <summary>
    /// Indica se il valore è stato convertito correttamente.
    /// </summary>
    public bool IsParsed { get; }

    /// <summary>
    /// Messaggio di errore eventuale (vuoto se non ci sono errori).
    /// </summary>
    public string ErrorMessage { get; private set; }

    /// <summary>
    /// Costruttore principale:
    /// 1) Memorizza la stringa di origine (Unparsed).
    /// 2) Esegue il parsing, eventualmente tramite conversionMethod custom.
    /// 3) Se il parsing fallisce, IsParsed viene impostato a false.
    /// </summary>
    public ParsedUnparsed(string columnName, string unparsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName;
        Unparsed = unparsed;
        IsParsed = true;
        ErrorMessage = string.Empty;

        try
        {
            // Workaround: se "TesseraN" ha valore "staff", impostiamo default
            if (ColumnName == "TesseraN" && unparsed == "staff")
            {
                Parsed = default;
            }
            else
            {
                Parsed = conversionMethod != null
                    ? conversionMethod(unparsed)
                    : ParseStringToValue(unparsed);
            }
        }
        catch (Exception e)
        {
            Parsed = default;
            IsParsed = false;
            ErrorMessage = $"Errore durante il parsing della colonna '{ColumnName}', valore {unparsed}: {e.Message}";
        }
    }

    /// <summary>
    /// Costruttore alternativo: permette di creare l'oggetto avendo già il valore parsed.
    /// </summary>
    public ParsedUnparsed(string columnName, T parsed)
    {
        ColumnName = columnName;
        Parsed = parsed;
        Unparsed = parsed?.ToString();
        IsParsed = true;
        ErrorMessage = string.Empty;
    }

    /// <summary>
    /// Tenta di convertire la stringa in T, gestendo casi speciali (DateTime, bool).
    /// Se non è un tipo specializzato, usa TypeDescriptor.
    /// </summary>
    protected static T ParseStringToValue(string cellValue)
    {
        // Parsing specializzato per DateTime
        if (typeof(T) == typeof(DateTime))
        {
            return (T)(object)ParseDate(cellValue);
        }

        // Parsing specializzato per bool / bool?
        if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
        {
            return (T)(object)ParseBoolean(cellValue);
        }

        // Generic fallback con TypeDescriptor (gestisce int, decimal, double, ecc.)
        var converter = TypeDescriptor.GetConverter(typeof(T));
        if (!string.IsNullOrEmpty(cellValue) && converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return (T)converter.ConvertFromString(cellValue);
        }

        return default;
    }

    /// <summary>
    /// Parsing per le date, supportando formati multipli prima di tentare un parse generico.
    /// </summary>
    private static DateTime ParseDate(string cellValue)
    {
        var enUS = new CultureInfo("en-US");
        var formats = new[]
        {
            "yyyy-MM-dd HH.mm.ss",
            "M/d/yyyy HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy"
        };

        // Primo tentativo: parse con formati specifici
        if (DateTime.TryParseExact(cellValue, formats, enUS, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Secondo tentativo: parse generico
        if (DateTime.TryParse(cellValue, out result))
        {
            return result;
        }

        throw new FormatException($"{cellValue} non è una data valida");
    }

    /// <summary>
    /// Parsing di stringa in bool? con set personalizzabili di valori che rappresentano true/false.
    /// </summary>
    private static bool? ParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null; // Se vuoto o null, restituisce null
        }

        var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Sì", "Yes", "True", "V", "X", "1"
        };
        var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "No", "False", "0"
        };

        if (trueValues.Contains(value)) return true;
        if (falseValues.Contains(value)) return false;

        throw new FormatException($"Il valore '{value}' non può essere convertito in bool.");
    }

    /// <summary>
    /// Rappresentazione stringa per debug/log:
    /// se T non è string, mostra "Parsed (Unparsed)" se differiscono.
    /// </summary>
    public override string ToString()
    {
        // Se T è già string, ritorna Unparsed così com'è.
        if (typeof(T) == typeof(string))
        {
            return Unparsed;
        }

        // Se T non è string e Parsed è null
        if (Parsed == null)
        {
            return Unparsed;
        }

        // Se i valori differiscono, mostra "Parsed (Unparsed)"
        if (!Parsed.Equals(Unparsed) && !string.IsNullOrEmpty(Unparsed))
        {
            return $"{Parsed} ({Unparsed})";
        }

        // Altrimenti mostra solo Parsed
        return Parsed.ToString();
    }
}