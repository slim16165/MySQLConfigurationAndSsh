using System;
using System.ComponentModel;
using System.Globalization;

namespace MySQLConfigurationAndSsh.Parser;

public class ParsedUnparsed<T>
{
    public string ColumnName { get; }
    public string Unparsed { get; }
    public T Parsed { get; set; }
    public bool IsParsed { get; }


    public ParsedUnparsed(string columnName, string unparsed, Func<string, T> conversionMethod = null)
    {
        ColumnName = columnName;
        Unparsed = unparsed;
        try
        {
            Parsed = conversionMethod != null ? conversionMethod(unparsed) : ParseStringToValue<T>(unparsed);
        }
        catch (Exception e)
        {
            Parsed = default;
            IsParsed = false;
        }
    }


    protected static T ParseStringToValue<T>(string cellValue)
    {
        //https://github.com/deniszykov/TypeConversion
        //https://github.com/t-bruning/UniversalTypeConverter
        if (typeof(T) == typeof(DateTime))
            return (T)(object)ParseDate(cellValue);

        var converter = TypeDescriptor.GetConverter(typeof(T));
        if (converter != null)
        {
            return (T)converter.ConvertFromString(cellValue);
        }
        return default;
    }


    private static T ParseDate(string cellValue)
    {
        CultureInfo enUS = new CultureInfo("en-US");
        if (DateTime.TryParseExact(cellValue, "yyyy-MM-dd HH.mm.ss", enUS, DateTimeStyles.None,
                out DateTime result))
            return (T)(object)result;
        else throw new Exception(cellValue + "non è una data valida");
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