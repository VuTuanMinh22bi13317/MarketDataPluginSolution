namespace MarketDataPlugin
{
    public abstract class FixItem
    {
        public string Tag { get; }
        public string Name { get; }

        protected FixItem(string tag, string name)
        {
            Tag = tag ?? "";
            Name = name ?? "Unknown";
        }

        public abstract void Parse(string fixMessage);
    }
}