using System.Collections.Generic;
using MarketDataPlugin.DEF; // Added to resolve LoopDef

namespace MarketDataPlugin
{
    public class Loop
    {
        public string Counter { get; }
        public string Name { get; }
        public List<MessageField> ParsedFields { get; } = new List<MessageField>();

        public Loop(string counter, string name, string count)
        {
            Counter = counter ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public Loop(string counter, string name, List<TVP> pairs, int startIdx)
        {
            Counter = counter ?? string.Empty;
            Name = name ?? string.Empty;
            for (int i = startIdx; i < pairs.Count && pairs[i].Tag != "10"; i++)
            {
                string value = pairs[i].Value ?? string.Empty; // Handle null value
                ParsedFields.Add(new MessageField { Tag = pairs[i].Tag, Value = value });
            }
        }

        // Constructor to match supervisor's design
        public Loop(Msg parent, LoopDef def, List<TVP> pairs, int startIdx)
        {
            Counter = def.Counter ?? string.Empty;
            Name = def.Name ?? string.Empty;
            for (int i = startIdx; i < pairs.Count && pairs[i].Tag != "10"; i++)
            {
                string value = pairs[i].Value ?? string.Empty;
                ParsedFields.Add(new MessageField { Tag = pairs[i].Tag, Value = value });
            }
        }

        public void AddParsedField(MessageField field) => ParsedFields.Add(field ?? throw new ArgumentNullException(nameof(field)));
    }
}