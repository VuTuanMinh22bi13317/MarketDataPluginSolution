namespace MarketDataPlugin
{
    using System;

    // Represents a Tag-Value Pair in a FIX message
    public class TVP
    {
        // Fields
        private string _raw;

        // Properties
        public bool HasTag { get; private set; }
        public bool HasValue { get; private set; }
        public string Raw
        {
            get => _raw;
            set => _raw = value ?? string.Empty;
        }
        public int Tag { get; private set; } // Changed to int
        public string Value { get; private set; }

        // Constructor
        public TVP()
        {
            _raw = string.Empty;
            HasTag = false;
            HasValue = false;
            Tag = 0; // Default for int
            Value = string.Empty;
        }

        // Overloaded Constructor
        public TVP(string rawValue)
        {
            _raw = rawValue ?? string.Empty;
            ParseRaw();
        }

        // Parse the raw string into Tag and Value
        public void FromString(string rawValue)
        {
            _raw = rawValue ?? string.Empty;
            ParseRaw();
        }

        // Convert the TVP to a string representation
        public override string ToString()
        {
            return HasTag && HasValue ? $"{Tag}={Value}" : string.Empty;
        }

        // Private helper method to parse the raw string
        private void ParseRaw()
        {
            if (string.IsNullOrEmpty(_raw))
            {
                HasTag = false;
                HasValue = false;
                Tag = 0;
                Value = string.Empty;
                return;
            }

            int equalsIndex = _raw.IndexOf('=');
            if (equalsIndex > 0 && equalsIndex < _raw.Length - 1)
            {
                string tagStr = _raw.Substring(0, equalsIndex);
                Value = _raw.Substring(equalsIndex + 1);

                try
                {
                    Tag = int.Parse(tagStr);
                    HasTag = true;
                    HasValue = !string.IsNullOrEmpty(Value);
                }
                catch (FormatException)
                {
                    Console.WriteLine($"Failed to parse tag as integer: {tagStr}");
                    HasTag = false;
                    HasValue = false;
                    Tag = 0;
                    Value = string.Empty;
                }
            }
            else
            {
                HasTag = false;
                HasValue = false;
                Tag = 0;
                Value = string.Empty;
            }
        }
    }
}