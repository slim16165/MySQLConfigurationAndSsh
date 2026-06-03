using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace MySQLConfigurationAndSsh.Parser;

public class ParsedUnparsed<T>
{
    public string ColumnName { get; }

    public string Unparsed { get; }

    public T Parsed { get; set; }

    public bool IsParsed { get; }

    public string ErrorMessage { get; }

    public ParsedUnparsed(string unparsed, Func<string, T> conversionMethod = null)
        : this(string.Empty, unparsed, conversionMethod)
    {
    }

    public ParsedUnparsed(string columnName, string unparsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName ?? string.Empty;
        Unparsed = unparsed;

        try
        {
            if (ColumnName == "TesseraN" && unparsed == "staff")
            {
                Parsed = default;
                IsParsed = true;
                ErrorMessage = string.Empty;
                return;
            }

            if (conversionMethod != null)
            {
                Parsed = conversionMethod(unparsed);
                IsParsed = true;
                ErrorMessage = string.Empty;
                return;
            }

            var (parsedValue, errorMessage) = TryParseStringToValue(unparsed);
            Parsed = parsedValue;
            IsParsed = errorMessage == null;
            ErrorMessage = errorMessage == null
                ? string.Empty
                : $"[{ColumnName}] Parsing fallito per valore '{unparsed}': {errorMessage}";

            if (!IsParsed)
            {
                Debug.WriteLine(ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Parsed = default;
            IsParsed = false;
            ErrorMessage = $"[{ColumnName}] ERRORE inaspettato nel parsing di '{unparsed}': {ex.Message}";
            Debug.WriteLine(ErrorMessage);
        }
    }

    public ParsedUnparsed(string columnName, T parsed)
    {
        ColumnName = columnName ?? string.Empty;
        Parsed = parsed;
        Unparsed = parsed?.ToString();
        IsParsed = true;
        ErrorMessage = string.Empty;
    }

    private static (T parsedValue, string errorMessage) TryParseStringToValue(string cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
        {
            return (default, null);
        }

        try
        {
            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            {
                return ParseDate(cellValue);
            }

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                return ParseBoolean(cellValue);
            }

            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                return ((T)converter.ConvertFromString(cellValue), null);
            }

            if (typeof(T) == typeof(string))
            {
                return ((T)(object)cellValue, null);
            }

            return (default, $"Nessun convertitore trovato per il tipo {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }

    private static (T parsedValue, string errorMessage) ParseDate(string cellValue)
    {
        var enUs = new CultureInfo("en-US");
        var formats = new[]
        {
            "yyyy-MM-dd HH.mm.ss",
            "M/d/yyyy HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy"
        };

        if (DateTime.TryParseExact(cellValue, formats, enUs, DateTimeStyles.None, out var date))
        {
            return CastDateTime(date);
        }

        if (DateTime.TryParse(cellValue, out date))
        {
            return CastDateTime(date);
        }

        return (default, $"'{cellValue}' non è una data valida");
    }

    private static (T parsedValue, string errorMessage) CastDateTime(DateTime date)
    {
        if (typeof(T) == typeof(DateTime))
        {
            return ((T)(object)date, null);
        }

        if (typeof(T) == typeof(DateTime?))
        {
            return ((T)(object)(DateTime?)date, null);
        }

        return (default, $"Il tipo {typeof(T).Name} non è compatibile con DateTime");
    }

    private static (T parsedValue, string errorMessage) ParseBoolean(string value)
    {
        var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Si", "Sì", "Yes", "True", "V", "X", "1"
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

        if (string.IsNullOrWhiteSpace(value) && typeof(T) == typeof(bool?))
        {
            return ((T)(object)null, null);
        }

        return (default, $"Il valore '{value}' non può essere convertito in boolean");
    }

    private static (T parsedValue, string errorMessage) CastBool(bool value)
    {
        if (typeof(T) == typeof(bool))
        {
            return ((T)(object)value, null);
        }

        if (typeof(T) == typeof(bool?))
        {
            return ((T)(object)(bool?)value, null);
        }

        return (default, $"Il tipo {typeof(T).Name} non è compatibile con bool");
    }

    public override string ToString()
    {
        if (typeof(T) == typeof(string))
        {
            return Unparsed;
        }

        if (Parsed == null)
        {
            return Unparsed;
        }

        if (!Parsed.Equals(Unparsed) && !string.IsNullOrEmpty(Unparsed))
        {
            return $"{Parsed} ({Unparsed})";
        }

        return Parsed.ToString();
    }
}
