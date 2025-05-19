namespace MarketDataPlugin
{
    using System;
    using System.Collections.Generic;

    public class MsgDef : FixItem
    {
        public string MsgType { get; set; }
        public DataDirection Direction { get; set; } // Added to match FixParser.cs
        public List<MsgChildDef> Children { get; set; }

        public MsgDef(int tag, string name, string msgType, DataDirection direction)
            : base(tag, name ?? throw new ArgumentNullException(nameof(name)))
        {
            MsgType = msgType ?? throw new ArgumentNullException(nameof(msgType));
            Direction = direction;
            Children = new List<MsgChildDef>();
        }

        public void AddChild(MsgChildDef child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            Children.Add(child);
        }

        public override void Parse(string data)
        {
            // MsgDef is a definition, not a runtime instance, so no parsing is needed
        }
    }
}