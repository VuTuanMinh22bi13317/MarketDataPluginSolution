using System.Collections.Generic;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Loop
    {
        public string Tag { get; }
        public string Name { get; }
        public string Counter { get; }
        public Dictionary<string, FieldDef> Children { get; } = new Dictionary<string, FieldDef>();

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
    }
}