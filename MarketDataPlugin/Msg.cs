using System.Collections.Generic;
using System.Text;

namespace MarketDataPlugin
{
    public class Msg
    {
        public string Name { get; }
        public string MsgType { get; }
        public DataDirection Direction { get; }
        private readonly Dictionary<string, object> children = new Dictionary<string, object>();

        public IReadOnlyDictionary<string, object> Children => children.AsReadOnly();

        public Msg(string name, string msgType, DataDirection direction)
        {
            Name = name;
            MsgType = msgType;
            Direction = direction;
        }

        public void AddChild(string key, object child)
        {
            children[key] = child;
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendLine($"Msg: Name={Name}, MsgType={MsgType}, Direction={Direction}, Children={Children.Count}");
            foreach (var child in Children)
            {
                if (child.Value is FieldDef fieldDef)
                {
                    string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                        ? $", DateFormat={fieldDef.DateFormat}"
                        : "";
                    result.AppendLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}, IsLog={fieldDef.IsLog}{dateFormatDisplay}");
                }
                else if (child.Value is LoopDef loopDef)
                {
                    result.AppendLine(loopDef.ToString());
                }
            }
            return result.ToString();
        }
    }
}