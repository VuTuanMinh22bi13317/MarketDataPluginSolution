namespace MarketDataPlugin
{
    public class FieldDef : MsgChildDef
    {
        public FieldType Type { get; set; }
        public MsgChildDef? ChildDef { get; set; }

        public FieldDef(int tag, string name, FieldType type, bool required)
            : base(tag, name, required)
        {
            Type = type;
        }

        public override void Parse(string data)
        {
            // Implement field parsing logic if needed
        }
    }
}