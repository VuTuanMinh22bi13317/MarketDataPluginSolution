namespace MarketDataPlugin
{
    public class Specs
    {
        public string BeginString { get; set; }
        public bool InMsgChecksum { get; set; }
        public bool OutMsgChecksum { get; set; }
        public int TimeZone { get; set; }
        public List<object> Messages { get; } = new List<object>();
        public List<object> Loops { get; } = new List<object>();

        public Specs(string majorVersion, string minorVersion)
        {
            BeginString = $"FIX.{majorVersion}.{minorVersion}";
        }

        public DataDirection GetDirection(string directionStr)
        {
            return directionStr.ToLower() switch
            {
                "in" => DataDirection.In,
                "out" => DataDirection.Out,
                "inbound" => DataDirection.Inbound,
                "outbound" => DataDirection.Outbound,
                _ => DataDirection.In // Default to In
            };
        }
    }
}