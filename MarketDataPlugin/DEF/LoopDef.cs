using System;
using System.Collections.Generic;

namespace MarketDataPlugin.DEF
{
    public class LoopDef
    {
        public string Tag { get; }
        public string Name { get; }
        public string? Counter { get; }
        private readonly Dictionary<string, FieldDef> children = new Dictionary<string, FieldDef>();

        public LoopDef(string tag, string name, string? counter)
        {
            Tag = tag;
            Name = name;
            Counter = counter;
        }

        public void AddChild(string tag, FieldDef field)
        {
            children[tag] = field;
        }

        public IReadOnlyDictionary<string, FieldDef> GetChildren()
        {
            return children.AsReadOnly();
        }
    }
}