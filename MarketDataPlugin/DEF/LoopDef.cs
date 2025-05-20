using MarketDataPlugin.Interfaces;

namespace MarketDataPlugin
{
    public class LoopDef : ILoopDef
    {
        public string Tag { get; }
        public string Name { get; }
        public string Counter { get; }
        public bool Required { get; }
        private readonly List<object> children = new List<object>();
        public IReadOnlyList<object> Children => children.AsReadOnly();

        public LoopDef(string tag, string name, string counter, bool required)
        {
            Tag = tag;
            Name = name;
            Counter = counter;
            Required = required;
        }

        public void AddChild(object child)
        {
            children.Add(child);
        }
    }
}