using System.Collections.Generic;

namespace MarketDataPlugin
{
    public class Loop
    {
        public string Counter { get; }
        public string Name { get; }
        public List<MessageField> ParsedFields { get; } = new List<MessageField>();

        public Loop(string counter, string name, string count)
        {
            Counter = counter;
            Name = name;
        }

        public Loop(string counter, string name, List<TVP> pairs, int startIdx)
        {
            Counter = counter;
            Name = name;
            for (int i = startIdx; i < pairs.Count && pairs[i].Tag != "10"; i++)
            {
                ParsedFields.Add(new MessageField { Tag = pairs[i].Tag, Value = pairs[i].Value });
            }
        }

        public void AddParsedField(MessageField field) => ParsedFields.Add(field);
    }
}