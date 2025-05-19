namespace MarketDataPlugin
{
    public class Field : MsgChildDef
    {
        public FieldType Type { get; }
        public bool Required { get; }
        public string Value { get; set; }

        public Field(string tag, string name, FieldType type, bool required) : base(tag, name)
        {
            Type = type;
            Required = required;
            Value = "";
        }

        public override void Parse(string fixMessage)
        {
            // Expecting fixMessage in the format "tag=value" (e.g., "55=ABC")
            if (string.IsNullOrEmpty(fixMessage))
                throw new ArgumentException("FIX message is empty.");

            var parts = fixMessage.Split('=');
            if (parts.Length != 2 || parts[0] != Tag)
                throw new ArgumentException($"Invalid FIX message for field {Name}: {fixMessage}");

            Value = parts[1]; // Store the value
        }
    }
}