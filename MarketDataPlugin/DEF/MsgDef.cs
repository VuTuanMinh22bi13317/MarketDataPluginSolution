using MarketDataPlugin;

namespace MarketDataPlugin
{
    public class MsgDef : FixItem
    {
        public string MsgType { get; }
        public DataDirection Direction { get; }
        public List<object> Children { get; } = new List<object>();

        public MsgDef(string name, string msgType, DataDirection direction) : base("", name)
        {
            MsgType = msgType;
            Direction = direction;
        }

        public void AddChild(object child) => Children.Add(child);

        public override void Parse(string fixMessage)
        {
            throw new NotImplementedException("Parse is not implemented for MsgDef; use Msg class for runtime parsing.");
        }
    }
}