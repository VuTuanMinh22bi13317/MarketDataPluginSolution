using System.Collections.Generic;

namespace MarketDataPlugin.DEF
{
    // LoopDef class defines the structure of a repeating group (loop) in a FIX message.
    // It stores the loop counter, name, and child fields.
    public class LoopDef
    {
        public string? Counter { get; set; } // The tag that indicates the start of a loop (e.g., "83")
        public string Name { get; set; } = string.Empty; // Name of the loop, initialized to avoid null warnings
        public Dictionary<string, FieldDef> Children { get; } = new Dictionary<string, FieldDef>(); // Child fields within the loop

        // Constructor: Initializes a LoopDef with a counter, name, and child fields.
        // Parameters:
        // - counter: The tag that starts the loop.
        // - name: The name of the loop.
        // - children: Dictionary of child fields (tag to FieldDef mapping).
        public LoopDef(string counter, string name, Dictionary<string, FieldDef> children)
        {
            Counter = counter;
            Name = name;
            foreach (var child in children)
            {
                Children.Add(child.Key, child.Value);
            }
        }

        // AddChild: Adds a child field to the loop if it doesn't already exist.
        // Parameters:
        // - key: The tag of the child field.
        // - child: The FieldDef object for the child field.
        public void AddChild(string key, FieldDef child)
        {
            if (!Children.ContainsKey(key)) Children.Add(key, child);
        }

        // GetChildren: Returns the child fields as a dictionary of objects.
        // Returns: A dictionary mapping tags to child field definitions.
        public Dictionary<string, object> GetChildren()
        {
            var result = new Dictionary<string, object>();
            foreach (var child in Children)
            {
                result.Add(child.Key, child.Value);
            }
            return result;
        }
    }
}