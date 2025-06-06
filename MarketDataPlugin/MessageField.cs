namespace MarketDataPlugin
{
    public class MessageField
    {
        public string Tag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DataField { get; set; } = string.Empty;
        public object ParsedValue { get; set; } = string.Empty;
    }
}