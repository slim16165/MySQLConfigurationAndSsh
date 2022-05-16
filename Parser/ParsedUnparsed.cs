using System;
using System.Globalization;

namespace MySQLConfigurationAndSsh.Parser
{
    public class ParsedUnparsed<T>
    {
        public string Unparsed { get; }
        public T Parsed { get; set; }

        public ParsedUnparsed(string unparsed, Func<string, T> conversionMethod = null)
        {
            Unparsed = unparsed;
            try
            {
                if (conversionMethod != null)
                    Parsed = conversionMethod(unparsed);
                else
                    Parsed = ParseStringToValue(unparsed);
            }
            catch (Exception e)
            {
                Parsed = default;
            }
        }


        protected static T ParseStringToValue(string cellValue)
        {
            if (typeof(T) == typeof(DateTime))
                return ParseDate(cellValue);

            if (typeof(T) == typeof(decimal))
                return (T)(object)Convert.ToDecimal(cellValue);

            if (typeof(T) == typeof(double))
                return (T)(object)Convert.ToDouble(cellValue);

            if (typeof(T) == typeof(int))
                return (T)(object)Convert.ToInt32(cellValue);

            if (typeof(T) == typeof(int?))
                return (T)(object)Convert.ToInt32(cellValue);

            return (T)(object)cellValue;
        }

        private static T ParseDate(string cellValue)
        {
            CultureInfo enUS = new CultureInfo("en-US");
            if (DateTime.TryParseExact(cellValue, "yyyy-MM-dd HH.mm.ss", enUS, DateTimeStyles.None,
                out DateTime prova1))
                return (T)(object)prova1;
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
}