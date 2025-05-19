namespace MarketDataPlugin
{
    public class Msg : MsgChildDef
    {
        public List<Field> Fields { get; } = new List<Field>();
        public List<Loop> Loops { get; } = new List<Loop>();

        public Msg(string name, MsgDef definition) : base("", name)
        {
            foreach (var child in definition.Children)
            {
                if (child is FieldDef fieldDef)
                {
                    Fields.Add(new Field(fieldDef.Tag, fieldDef.Name, fieldDef.Type, fieldDef.Required));
                }
                else if (child is LoopDef loopDef)
                {
                    Loops.Add(new Loop(loopDef.Name, loopDef));
                }
            }
        }

        public bool IsMsgType(string msgType) => msgType == Tag;

        public override void Parse(string fixMessage)
        {
            // Expecting fixMessage in the format "field1^field2^..." (e.g., "35=d^55=ABC^268=2...")
            if (string.IsNullOrEmpty(fixMessage))
                throw new ArgumentException("FIX message is empty.");

            var parts = fixMessage.Split('^');
            if (parts.Length == 0)
                throw new ArgumentException($"Invalid FIX message for msg {Name}: {fixMessage}");

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