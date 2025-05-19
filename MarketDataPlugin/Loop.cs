namespace MarketDataPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    // Represents an instance of a loop in a FIX message
    public class Loop : FixItem
    {
        private readonly List<FixItem> _children;

        // List of child items within the loop instance
        public ReadOnlyCollection<FixItem> Children => _children.AsReadOnly();

        // Reference to the loop definition (optional)
        public LoopDef? Definition { get; set; }

        // Constructor to initialize loop properties
        public Loop(string name, LoopDef? definition = null)
            : base(0, name ?? throw new ArgumentNullException(nameof(name))) // Updated for int Tag
        {
            _children = new List<FixItem>();
            Definition = definition;
        }

        // Add a child item to the loop instance
        public void AddChild(FixItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            _children.Add(child);
        }

        // Recursively get all fields within the loop and its nested loops
        public IEnumerable<Field> GetAllFields()
        {
            foreach (var child in _children)
            {
                if (child is Field field)
                    yield return field;
                else if (child is Loop nestedLoop)
                {
                    foreach (var nestedField in nestedLoop.GetAllFields())
                        yield return nestedField;
                }
            }
        }

        // Parse the loop data from a FIX message
        public override void Parse(string data)
        {
            if (Definition == null || string.IsNullOrEmpty(data))
                return;

            var tvps = data.Split('\x01', StringSplitOptions.RemoveEmptyEntries)
                .Select(tvpStr => new TVP(tvpStr))
                .Where(tvp => tvp.HasTag && tvp.HasValue)
                .ToList();

            // Find the counter tag (e.g., 268 for NoMDEntries)
            int counter = 0;
            var counterTvp = tvps.FirstOrDefault(tvp => tvp.Tag == Definition.Tag); // Tag is int
            if (counterTvp != null)
            {
                if (int.TryParse(counterTvp.Value, out int count))
                {
                    counter = count;
                }
                tvps.Remove(counterTvp); // Remove the counter TVP from further processing
            }
            else
            {
                Console.WriteLine($"Counter tag {Definition.Tag} not found in message.");
                return;
            }

            // Parse repeating groups based on the counter
            for (int i = 0; i < counter; i++)
            {
                // For each iteration, create a set of fields based on the LoopDef
                foreach (var defChild in Definition.Children) // Changed from defChild.Item
                {
                    if (defChild is FieldDef fieldDef)
                    {
                        var fieldTvp = tvps.FirstOrDefault(tvp => tvp.Tag == fieldDef.Tag); // Tag is int
                        if (fieldTvp != null)
                        {
                            var field = new Field(fieldDef.Tag, fieldDef.Name, fieldDef.Type, fieldDef.Required);
                            field.FromString(fieldTvp.Value);
                            AddChild(field);
                            tvps.Remove(fieldTvp); // Remove processed TVP
                        }
                    }
                    else if (defChild is LoopDef nestedLoopDef)
                    {
                        var nestedLoop = new Loop(nestedLoopDef.Name, nestedLoopDef);
                        nestedLoop.Parse(data); // Recursive parsing for nested loops
                        AddChild(nestedLoop);
                    }
                }
            }
        }
    }
}