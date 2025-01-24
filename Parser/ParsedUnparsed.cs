using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using NLog;

namespace MySQLConfigurationAndSsh.Parser;

/// <summary>
/// Rappresenta un valore letto come stringa (Unparsed)
/// e convertito in un tipo forte (Parsed).
/// Utilizzato per gestire conversioni, errori di parsing e valori speciali.
/// </summary>
public class ParsedUnparsed<T>
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
    public string ErrorMessage { get; }

    /// <summary>
    /// Costruttore principale:
    /// 1) Memorizza la stringa di origine (Unparsed).
    /// 2) Esegue il parsing, eventualmente tramite conversionMethod custom.
    /// 3) Se il parsing fallisce, IsParsed viene impostato a false e viene popolato ErrorMessage.
    /// </summary>
    public ParsedUnparsed(string columnName, string unparsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName;
        Unparsed = unparsed;

        try
        {
            // Caso particolare per "TesseraN" con valore "staff"
            // Evitiamo eccezioni e settiamo "Parsed = default" se vogliamo ignorare il valore
            if (ColumnName == "TesseraN" && unparsed == "staff")
            {
                Parsed = default;
                IsParsed = true;
                ErrorMessage = string.Empty;
                return;
            }

            // Se c'è un metodo custom per la conversione, usalo
            if (conversionMethod != null)
            {
                Parsed = conversionMethod(unparsed);
                IsParsed = true;
                ErrorMessage = string.Empty;
                return;
            }

            // Altrimenti usa il parsing standard
            var (parsedValue, errorMsg) = TryParseStringToValue(unparsed);
            if (errorMsg == null)
            {
                Parsed = parsedValue;
                IsParsed = true;
                ErrorMessage = string.Empty;
            }
            else
            {
                Parsed = default;
                IsParsed = false;
                ErrorMessage = $"[{ColumnName}] Parsing fallito per valore '{unparsed}': {errorMsg}";
                logger.Warn(ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            // Catch generico se qualcosa sfugge: evitiamo di rilanciare e logghiamo
            Parsed = default;
            IsParsed = false;
            ErrorMessage = $"[{ColumnName}] ERRORE inaspettato nel parsing di '{unparsed}': {ex.Message}";
            logger.Error(ex, ErrorMessage); // log con stack trace
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
    /// Tenta di convertire la stringa in T, restituendo una tuple:
    /// (parsedValue, errorMessage).
    /// Se errorMessage è non-null, il parsing è fallito.
    /// </summary>
    private static (T parsedValue, string errorMessage) TryParseStringToValue(string cellValue)
    {
        // Se la stringa è vuota o null, restituiamo default(T) come valore di fallback
        if (string.IsNullOrWhiteSpace(cellValue))
        {
            return (default, null);
        }

        try
        {
            // Parsing specializzato per DateTime / DateTime?
            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            {
                return ParseDate(cellValue);
            }

            // Parsing specializzato per bool / bool?
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                return ParseBoolean(cellValue);
            }

            // Generic fallback con TypeDescriptor (gestisce int, decimal, double, ecc.)
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                var converted = (T)converter.ConvertFromString(cellValue);
                return (converted, null);
            }

            // Se non siamo riusciti a gestire, restituiamo errore
            return (default, $"Nessun convertitore trovato per il tipo {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            // Non rilanciamo l'eccezione, ma la trasformiamo in un messaggio di errore
            return (default, ex.Message);
        }
    }

    /// <summary>
    /// Parsing di stringa in DateTime/DateTime? (con vari formati).
    /// </summary>
    private static (T parsedValue, string errorMessage) ParseDate(string cellValue)
    {
        var enUS = new CultureInfo("en-US");
        var formats = new[]
        {
            "yyyy-MM-dd HH.mm.ss",
            "M/d/yyyy HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy"
        };

        // Tenta con i formati definiti
        if (DateTime.TryParseExact(cellValue, formats, enUS, DateTimeStyles.None, out var dt))
        {
            return CastDateTime(dt);
        }

        // Tenta un parse generico
        if (DateTime.TryParse(cellValue, out dt))
        {
            return CastDateTime(dt);
        }

        return (default, $"'{cellValue}' non è una data valida");
    }

    private static (T parsedValue, string errorMessage) CastDateTime(DateTime dt)
    {
        // Se T è DateTime, restituiamo come DateTime
        if (typeof(T) == typeof(DateTime))
        {
            return ((T)(object)dt, null);
        }

        // Se T è DateTime?, restituiamo come DateTime?
        if (typeof(T) == typeof(DateTime?))
        {
            return ((T)(object)(DateTime?)dt, null);
        }

        return (default, $"Il tipo {typeof(T).Name} non è compatibile con DateTime");
    }

    /// <summary>
    /// Parsing di stringa in bool / bool?, supportando un set di valori speciali (Sì, V, ecc.).
    /// </summary>
    private static (T parsedValue, string errorMessage) ParseBoolean(string value)
    {
        var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Sì", "Si", "Yes", "True", "V", "X", "1"
        };

        var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "No", "False", "0"
        };

        if (trueValues.Contains(value))
        {
            return CastBool(true);
        }
        if (falseValues.Contains(value))
        {
            return CastBool(false);
        }

        // Se il valore è "vuoto", potremmo decidere che bool? = null
        if (string.IsNullOrWhiteSpace(value) && typeof(T) == typeof(bool?))
        {
            return ((T)(object)null, null);
        }

        return (default, $"Il valore '{value}' non può essere convertito in boolean");
    }

    private static (T parsedValue, string errorMessage) CastBool(bool b)
    {
        // Se T è bool
        if (typeof(T) == typeof(bool))
        {
            return ((T)(object)b, null);
        }

        // Se T è bool?
        if (typeof(T) == typeof(bool?))
        {
            return ((T)(object)(bool?)b, null);
        }

        return (default, $"Il tipo {typeof(T).Name} non è compatibile con bool");
    }

    /// <summary>
    /// Rappresentazione stringa per debug/log:
    /// se T non è string, mostra "Parsed (Unparsed)" se differiscono.
    /// </summary>
    public override string ToString()
    {
        // Se T è string, mostra direttamente Unparsed
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