namespace MarketDataPlugin
{
    public class Loop : MsgChildDef
    {
        public List<Field> Fields { get; } = new List<Field>();
        public List<Loop> Loops { get; } = new List<Loop>();

        public Loop(string name, LoopDef? definition = null) : base(definition?.Tag ?? "", name)
        {
            if (definition != null)
            {
                foreach (var child in definition.Children)
                {
                    if (child is FieldDef fieldDef)
                    {
                        Fields.Add(new Field(fieldDef.Tag, fieldDef.Name ?? "Unknown", fieldDef.Type, fieldDef.Required));
                    }
                    else if (child is LoopDef loopDef)
                    {
                        Loops.Add(new Loop(loopDef.Name ?? "Unknown", loopDef));
                    }
                }
            }
        }

        public override void Parse(string fixMessage)
        {
            // Expecting fixMessage to contain a repeating group (e.g., "268=2^269=0^270=100...")
            // Split by FIX delimiter (^) and process each field
            if (string.IsNullOrEmpty(fixMessage))
                throw new ArgumentException("FIX message is empty.");

            var parts = fixMessage.Split('^');
            if (parts.Length == 0)
                throw new ArgumentException($"Invalid FIX message for loop {Name}: {fixMessage}");

            foreach (var part in parts)
            {
                foreach (var field in Fields)
                {
                    try
                    {
                        field.Parse(part);
                    }
                    catch (ArgumentException) { /* Skip fields that don't match */ }
                }

                foreach (var loop in Loops)
                {
                    try
                    {
                        loop.Parse(part);
                    }
                    catch (ArgumentException) { /* Skip loops that don't match */ }
                }
            }
        }
    }
}