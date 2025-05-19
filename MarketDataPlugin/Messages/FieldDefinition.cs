using System;
using System.Globalization;

namespace MarketDataPlugin.Messages
{
    public class FieldDefinition
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public string DataField { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public string DateFormat { get; set; }

        public FieldDefinition(string tag, string name, string dataField, string type, bool required, string dateFormat)
        {
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataField = dataField ?? throw new ArgumentNullException(nameof(dataField));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Required = required;
            DateFormat = dateFormat ?? throw new ArgumentNullException(nameof(dateFormat));
        }

        public object ParseValue(string value, string suffix = "")
        {
            if (string.IsNullOrEmpty(value))
            {
                return DBNull.Value;
            }

            switch (Type.ToLower())
            {
                case "int":
                    if (int.TryParse(value, out int intValue))
                    {
                        return intValue;
                    }
                    break;
                case "double":
                    if (double.TryParse(value.Replace(",", "."), out double doubleValue))
                    {
                        return doubleValue;
                    }
                    break;
                case "date":
                case "datetime":
                    if (Tag == "60" && DateFormat == "HHmmssSSS") // TransactTime
                    {
                        if (value.Length == 9 && value.All(char.IsDigit))
                        {
                            string formattedTime = $"{value.Substring(0, 2)}:{value.Substring(2, 2)}:{value.Substring(4, 2)}.{value.Substring(6, 3)}";
                            if (DateTime.TryParseExact(formattedTime, "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                            {
                                return dateValue;
                            }
                        }
                    }
                    // Default date parsing (e.g., yyyyMMdd)
                    if (DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValueDefault))
                    {
                        return dateValueDefault;
                    }
                    break;
                case "string":
                default:
                    return value + suffix;
            }

            throw new FormatException($"Invalid format for {Type}: {value}");
        }
    }
}