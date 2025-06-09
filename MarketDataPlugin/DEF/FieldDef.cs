namespace MarketDataPlugin.DEF
{
    public class FieldDef : MsgChildDef
    {
        public FieldDef(string tag, string name) : base(tag, name)
        {
            Name = name; // Set the Name property of FieldDef
        }

        public new int Tag { get; set; } // Explicitly hide inherited Tag
        public new string Name { get; set; } = string.Empty; // Explicitly hide inherited Name
        public bool TimeZoneEffected { get; set; } = false;
        public DataDirection Direction { get; set; } = DataDirection.InOut;
        public string DataField { get; set; } = string.Empty;
        public int DataIdx { get; set; } = -1;
        public FieldType Type { get; set; } = FieldType.String;
        public string DateFormat { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public bool IsLog { get; set; } = true;
        public bool ToLoop { get; set; } = false;
        public bool IsRequired { get; set; } = false;
        public int TimeZone { get; set; } = 7;

        public override void Parse(string value)
        {
            throw new NotSupportedException("FieldDef does not support parsing directly.");
        }
    }
}