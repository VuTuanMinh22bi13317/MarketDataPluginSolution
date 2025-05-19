namespace MarketDataPlugin
{
    using System;
    using System.Collections.Generic;

    public class LoopDef : MsgChildDef
    {
        // List of child definitions within the loop
        public List<MsgChildDef> Children { get; set; }

        // The tag for the counter field (e.g., 268 for NO_MD_ENTRIES)
        public int Counter { get; set; }

        // Constructor to initialize loop definition properties
        public LoopDef(string name, int tag, int counter, bool required)
            : base(tag, name, required)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty or whitespace.", nameof(name));
            Counter = counter;
            Children = new List<MsgChildDef>();
        }

        // Add a child definition to the loop
        public void AddChild(MsgChildDef child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            Children.Add(child);
        }

        // Override Parse method (not used for definitions)
        public override void Parse(string data)
        {
            // LoopDef is a definition, not a runtime instance, so no parsing is needed
        }
    }
}