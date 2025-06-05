using System.Collections.Generic;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Loop
    {
        public string Tag { get; }
        public string Name { get; }
        public string Counter { get; set; }
        public Dictionary<string, FieldDef> Children { get; } = new Dictionary<string, FieldDef>(); // Definitions
        public List<MessageField> ParsedFields { get; } = new List<MessageField>(); // Parsed fields for this loop entry

        public Loop(string tag, string name, string counter)
        {
            Tag = tag;
            Name = name;
            Counter = counter;
        }

        public void AddChild(string key, FieldDef child)
        {
            if (!Children.ContainsKey(key)) Children.Add(key, child);
        }

        public void AddParsedField(MessageField field)
        {
            if (field != null) ParsedFields.Add(field);
        }
    }
}