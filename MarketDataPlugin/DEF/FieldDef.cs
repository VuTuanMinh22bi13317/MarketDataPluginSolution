namespace MarketDataPlugin
{
    public class FieldDef : FixItem
    {
        public FieldType Type { get; }
        public bool Required { get; }
        public string? DataField { get; }
        public bool IsLog { get; }

        public FieldDef(string tag, string name, FieldType type, bool required, string? dataField = null, bool isLog = false) : base(tag, name)
        {
            Type = type;
            Required = required;
            DataField = dataField;
            IsLog = isLog;
        }

        public override void Parse(string fixMessage)
        {
            throw new NotImplementedException("Parse is not implemented for FieldDef; use Field class for runtime parsing.");
        }
    }
}