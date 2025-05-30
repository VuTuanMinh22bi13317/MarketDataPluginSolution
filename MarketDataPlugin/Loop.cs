using System.Collections.Generic;
using System.Text;
using MarketDataPlugin.DEF; // Added to resolve FieldDef reference

namespace MarketDataPlugin
{
    public class Loop
    {
        public string Tag { get; }
        public string Name { get; }
        public string Counter { get; }
        private readonly Dictionary<string, object> children = new Dictionary<string, object>();

        public IReadOnlyDictionary<string, object> Children => children.AsReadOnly();

        public Loop(string tag, string name, string counter)
        {
            Tag = tag;
            Name = name;
            Counter = counter;
        }

        public void AddChild(string key, object child)
        {
            children[key] = child;
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendLine($"Loop: Tag={Tag}, Name={Name}, Counter={Counter}, Children={Children.Count}");
            foreach (var child in Children)
            {
                if (child.Value is FieldDef fieldDef)
                {
                    string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                        ? $", DateFormat={fieldDef.DateFormat}"
                        : "";
                    result.AppendLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}{dateFormatDisplay}");
                }
            }
            return result.ToString();
        }
    }
}