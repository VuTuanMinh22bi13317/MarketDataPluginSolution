namespace MarketDataPlugin
{
   

    public class FieldDef
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public FieldType Type { get; set; }
        public bool Required { get; set; }
        public string? DataField { get; set; } // Đổi thành nullable vì DataField có thể không có
        public bool IsLog { get; set; }
        public string? DateFormat { get; set; } // Đổi thành nullable vì DateFormat có thể là null

        public FieldDef(string tag, string name, FieldType type, bool required, string? dataField, bool isLog)
        {
            Tag = tag;
            Name = name;
            Type = type;
            Required = required;
            DataField = dataField;
            IsLog = isLog;
            DateFormat = null;
        }

        public FieldDef(string tag, string name, FieldType type, bool required, string? dataField, bool isLog, string? dateFormat)
            : this(tag, name, type, required, dataField, isLog)
        {
            DateFormat = dateFormat;
        }
    }
}