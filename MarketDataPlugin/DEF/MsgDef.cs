using System.Collections.Generic;

namespace MarketDataPlugin
{
    public class MsgDef
    {
        public string Name { get; }
        public string MsgType { get; }
        public DataDirection Direction { get; }
        private readonly Dictionary<string, object> children = new Dictionary<string, object>();

        public MsgDef(string name, string msgType, DataDirection direction)
        {
            Name = name;
            MsgType = msgType;
            Direction = direction;
        }

        public void AddChild(string key, object child)
        {
            children[key] = child;
        }

        public IReadOnlyDictionary<string, object> GetChildren()
        {
            return children.AsReadOnly();
        }
    }
}