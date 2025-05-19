namespace MarketDataPlugin
{
    using System;
    using System.Collections.Generic;

    public class Specs
    {
        public string? BeginString { get; set; } = "FIX.4.4"; // Explicitly nullable with default
        public bool InMsgChecksum { get; set; }
        public bool OutMsgChecksum { get; set; }
        public int TimeZone { get; set; }
        public List<MsgDef> Messages { get; set; }
        public List<LoopDef> Loops { get; set; }

        public Specs(string major, string minor)
        {
            BeginString = $"FIX.{major}.{minor}";
            InMsgChecksum = false;
            OutMsgChecksum = true;
            TimeZone = 7;
            Messages = new List<MsgDef>();
            Loops = new List<LoopDef>();
        }

        public Specs(TVP tvp)
        {
            BeginString = "FIX.4.4";
            InMsgChecksum = false;
            OutMsgChecksum = true;
            TimeZone = 7;
            Messages = new List<MsgDef>();
            Loops = new List<LoopDef>();
            Initialize(tvp);
        }

        public void Initialize(TVP tvp = null)
        {
            if (tvp != null && tvp.Tag == 8)
            {
                BeginString = tvp.Value ?? BeginString; // Safe with nullable types
            }
        }

        public Specs Create()
        {
            var parts = BeginString?.Split('.');
            if (parts == null || parts.Length < 3)
            {
                throw new ArgumentException("BeginString must be in the format FIX.major.minor[.servicePack]");
            }
            return new Specs(parts[1], parts[2]);
        }

        public DataDirection GetDirection(string direction)
        {
            return direction switch
            {
                "In" => DataDirection.Inbound,
                "Out" => DataDirection.Outbound,
                "InOut" => DataDirection.Inbound,
                _ => throw new ArgumentException($"Invalid direction: {direction}")
            };
        }
    }
}