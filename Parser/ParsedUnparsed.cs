using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace MySQLConfigurationAndSsh.Parser;

public class ParsedUnparsed<T>
{
    public string ColumnName { get; }
    public string Unparsed { get; }
    public T Parsed { get; set; }
    public bool IsParsed { get; }
    public string ErrorMessage { get; private set; }

    public ParsedUnparsed(string columnName, string unparsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName;
        Unparsed = unparsed;
        IsParsed = true;
        ErrorMessage = string.Empty;
        try
        {
            //Workaround
            if (ColumnName == "TesseraN" && unparsed == "staff")
            {
                Parsed = default(T);
            }
            else
                Parsed = conversionMethod != null ? conversionMethod(unparsed) : ParseStringToValue<T>(unparsed);
        }
        catch (Exception e)
        {
            Parsed = default;
            IsParsed = false;
            ErrorMessage = $"Errore durante il parsing della colonna '{ColumnName}', valore {unparsed}: {e.Message}";
        }
    }

    public ParsedUnparsed(string columnName, T parsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName;
        Unparsed = parsed.ToString();
        IsParsed = true;
        ErrorMessage = string.Empty;
        Parsed = parsed;
    }


    protected static T ParseStringToValue<T>(string cellValue)
    {
        //https://github.com/deniszykov/TypeConversion
        //https://github.com/t-bruning/UniversalTypeConverter
        if (typeof(T) == typeof(DateTime))
            return (T)(object)ParseDate(cellValue);

        if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
        {
            return (T)(object)ParseBoolean(cellValue);
        }

        var converter = TypeDescriptor.GetConverter(typeof(T));
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return (T)converter.ConvertFromString(cellValue);
        }
        return default;
    }

    //public static implicit operator ParsedUnparsed<T>(string value)
    //{
    //    return new ParsedUnparsed<T>(null, value); 
    //}


    private static T ParseDate(string cellValue)
    {
        CultureInfo enUS = new("en-US");
        string[] formats = { "yyyy-MM-dd HH.mm.ss", "M/d/yyyy HH:mm:ss" };
        if (DateTime.TryParseExact(cellValue, formats, enUS, DateTimeStyles.None, out DateTime result))
            return (T)(object)result;
        else throw new Exception(cellValue + " non è una data valida");
    }


    private static bool? ParseBoolean(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Aggiungi qui tutte le stringhe che vuoi mappare a true
        var trueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sì", "Yes", "True", "V", "X" };

        // Aggiungi qui tutte le stringhe che vuoi mappare a false
        var falseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "No", "False" };

        if (trueValues.Contains(value))
        {
            return true;
        }
        if (falseValues.Contains(value))
        {
            return false;
        }

        throw new FormatException($"Il valore '{value}' non può essere convertito in un booleano.");
    }

    public override string ToString()
    {
        if (typeof(T) != typeof(string))
        {
            if (Parsed.ToString() != Unparsed && Unparsed != null)
                return $"{Parsed} ({Unparsed})";
            else
                return Parsed.ToString();
        }

        return Unparsed;
    }
}