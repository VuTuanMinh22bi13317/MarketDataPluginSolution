namespace MarketDataPlugin
{
    public class FieldDef
    {
        public string Tag { get; }
        public string Name { get; }
        public FieldType Type { get; }
        public string? DataField { get; }
        public bool IsLog { get; }
        public string? DateFormat { get; }
        public bool IsRequired { get; }

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