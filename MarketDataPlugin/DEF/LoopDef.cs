using System.Collections.Generic;

namespace MarketDataPlugin
{
    public class LoopDef
    {
        public string Tag { get; }
        public string Name { get; }
        public string Counter { get; }
        private readonly Dictionary<string, object> children = new Dictionary<string, object>();

        public IReadOnlyDictionary<string, object> Children => children.AsReadOnly();

        public LoopDef(string tag, string name, string counter)
        {
            Tag = tag;
            Name = name;
            Counter = counter;
        }

        public void AddChild(string key, object child)
        {
            children[key] = child;
        }
    }
}