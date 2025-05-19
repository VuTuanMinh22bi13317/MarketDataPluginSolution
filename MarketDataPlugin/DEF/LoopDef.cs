namespace MarketDataPlugin
{
    public class LoopDef : FixItem
    {
        public string Counter { get; }
        public bool Required { get; }
        public List<object> Children { get; } = new List<object>();

        public LoopDef(string tag, string name, string counter, bool required) : base(tag, name)
        {
            Counter = counter;
            Required = required;
        }

        public void AddChild(object child) => Children.Add(child);

        public override void Parse(string fixMessage)
        {
            throw new NotImplementedException("Parse is not implemented for LoopDef; use Loop class for runtime parsing.");
        }
    }
}