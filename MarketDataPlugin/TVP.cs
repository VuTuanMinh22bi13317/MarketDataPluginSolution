namespace MarketDataPlugin
{
    public class TVP
    {
        public string? Tag { get; set; } // Made nullable to resolve CS8618
        public string? Value { get; set; } // Made nullable to resolve CS8618
        public string? Raw { get; set; } // Made nullable to resolve CS8618

        public static List<TVP> FromString(string raw)
        {
            var result = new List<TVP>();
            if (string.IsNullOrEmpty(raw)) return result;
            string[] pairs = raw.Split('\x01');
            foreach (var pair in pairs)
            {
                if (pair.Contains("="))
                {
                    var parts = pair.Split('=');
                    result.Add(new TVP { Tag = parts[0], Value = parts.Length > 1 ? parts[1] : "", Raw = pair });
                }
            }
            return result;
        }
    }
}