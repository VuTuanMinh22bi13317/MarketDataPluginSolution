using System.Collections.Generic;

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
            var result = new List<string> { $"Loop: Tag={Tag}, Name={Name}, Counter={Counter}, Children={Children.Count}" };
            foreach (var child in Children)
            {
                if (child.Value is FieldDef fieldDef)
                {
                    string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                        ? $", DateFormat={fieldDef.DateFormat}"
                        : "";
                    string isLogDisplay = (fieldDef.Tag.StartsWith("8") || fieldDef.Tag.StartsWith("9") || fieldDef.Tag.StartsWith("35") || fieldDef.Tag.StartsWith("49") || fieldDef.Tag.StartsWith("56") || fieldDef.Tag.StartsWith("34") || fieldDef.Tag.StartsWith("52")) ? $", IsLog={fieldDef.IsLog}" : "";
                    result.Add($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}{dateFormatDisplay}{isLogDisplay}");
                }
                else if (child.Value is LoopDef loopDef)
                {
                    result.Add(loopDef.ToString());
                }
            }
            return string.Join("\n", result);
        }
    }
}