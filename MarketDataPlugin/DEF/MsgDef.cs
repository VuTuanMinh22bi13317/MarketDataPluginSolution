using MarketDataPlugin.Interfaces;

namespace MarketDataPlugin
{
    public class MsgDef : IMessageDef
    {
        public string Name { get; }
        public string MsgType { get; }
        public DataDirection Direction { get; }
        private readonly List<object> children = new List<object>();
        public IReadOnlyList<object> Children => children.AsReadOnly();

        public MsgDef(string name, string msgType, DataDirection direction)
        {
            Name = name;
            MsgType = msgType;
            Direction = direction;
        }

        public void AddChild(object child)
        {
            children.Add(child);
        }
    }
}