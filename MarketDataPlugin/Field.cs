namespace MarketDataPlugin
{
    using System;

    // Represents a single FIX message field
    public class Field : FixItem
    {
        // Type of the field (e.g., String, Int, DateTime)
        public FieldType Type { get; set; }

        // Indicates if the field is required
        public bool Required { get; set; }

        // Field value as a raw string
        public string Raw { get; set; }

        // Field value (generic representation)
        public object? Value { get; set; } // Changed to nullable to resolve CS8618 and CS8625

        // Constructor to initialize field properties
        public Field(int tag, string name, FieldType type, bool required)
            : base(tag, name)
        {
            Type = type;
            Required = required;
            Raw = string.Empty;
            Value = null; // Safe with nullable type
        }

        // Parse raw string to appropriate type
        public void FromString(string rawValue)
        {
            Raw = rawValue;
            if (string.IsNullOrEmpty(rawValue))
            {
                Value = null; // Safe with nullable type (Line 27)
                return;
            }

            try
            {
                switch (Type)
                {
                    case FieldType.String:
                        Value = rawValue;
                        break;
                    case FieldType.Int:
                        if (int.TryParse(rawValue, out int intValue))
                            Value = intValue;
                        else
                            throw new FormatException($"Cannot parse '{rawValue}' as Int");
                        break;
                    case FieldType.DateTime:
                        if (DateTime.TryParse(rawValue, out DateTime dateValue))
                            Value = dateValue;
                        else if (DateTime.TryParseExact(rawValue, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dateValue))
                            Value = dateValue;
                        else
                            throw new FormatException($"Cannot parse '{rawValue}' as DateTime");
                        break;
                    case FieldType.Double:
                        if (double.TryParse(rawValue, out double doubleValue))
                            Value = doubleValue;
                        else
                            throw new FormatException($"Cannot parse '{rawValue}' as Double");
                        break;
                    case FieldType.Boolean:
                        if (bool.TryParse(rawValue, out bool boolValue))
                            Value = boolValue;
                        else
                            throw new FormatException($"Cannot parse '{rawValue}' as Boolean");
                        break;
                    default:
                        throw new NotSupportedException($"Field type {Type} is not supported.");
                }
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error parsing field {Name} (Tag {Tag}): {ex.Message}");
                Value = null; // Safe with nullable type (Line 36)
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine($"Error parsing field {Name} (Tag {Tag}): {ex.Message}");
                Value = null; // Safe with nullable type
            }
        }

        // Implement Parse method to align with FixItem
        public override void Parse(string data)
        {
            FromString(data);
        }

        // Convert field value to string
        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty; // Safe handling (Lines 80, 85)
        }
    }

    // Note: Remove this embedded enum if using the standalone FieldType.cs
    /*
    public enum FieldType
    {
        String,
        Int,
        DateTime,
        Double,
        Boolean
    }
    */
}