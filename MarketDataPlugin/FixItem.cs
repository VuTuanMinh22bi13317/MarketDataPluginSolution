namespace MarketDataPlugin
{
    // Abstract base class for all FIX message items
    public abstract class FixItem
    {
        public int Tag { get; set; } // Changed to int
        public string Name { get; set; }

        protected FixItem(int tag, string name)
        {
            Tag = tag; // No null check needed for int
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        // Abstract method to enforce parsing in subclasses
        public abstract void Parse(string data);
    }
}