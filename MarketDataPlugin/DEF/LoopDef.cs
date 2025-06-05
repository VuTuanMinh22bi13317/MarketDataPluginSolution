using System.Collections.Generic;

namespace MarketDataPlugin.DEF
{
    public class LoopDef
    {
        public string? Counter { get; set; }
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, FieldDef> Children { get; } = new Dictionary<string, FieldDef>();

        public LoopDef(string counter, string name, Dictionary<string, FieldDef> children)
        {
            Counter = counter;
            Name = name;
            foreach (var child in children)
            {
                Children.Add(child.Key, child.Value);
            }
        }

        public void AddChild(string key, FieldDef child)
        {
            if (!Children.ContainsKey(key)) Children.Add(key, child);
        }

        public Dictionary<string, FieldDef> GetChildren()
        {
            return new Dictionary<string, FieldDef>(Children);
        }
    }
}