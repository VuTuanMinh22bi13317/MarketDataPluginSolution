using System;

namespace MarketDataPlugin.DEF
{
    // Defines a single field in a FIX message with its tag, name, and type
    public class FieldDef
    {
        public string Tag { get; } // Tag number of the field
        public FieldType Type { get; } // Data type of the field
        public string Name { get; } // Name of the field
        public string? DataField { get; } // Associated data field identifier
        public bool IsLog { get; } // Flag indicating if field should be logged
        public string? DateFormat { get; } // Date format for Date type fields
        public bool IsRequired { get; } // Flag indicating if field is required

        // Creates a new FieldDef with the given details
        public FieldDef(string tag, string name, FieldType type, string? dataField, bool isLog, string? dateFormat, bool isRequired)
        {
            Tag = tag;
            Name = name;
            Type = type;
            DataField = dataField;
            IsLog = isLog;
            DateFormat = dateFormat;
            IsRequired = isRequired;
        }
    }
}