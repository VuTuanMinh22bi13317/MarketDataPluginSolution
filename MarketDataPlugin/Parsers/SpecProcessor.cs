using MarketDataPlugin;

namespace MarketDataPlugin.Parsers
{
    public class SpecProcessor
    {
        private readonly Specs specs;

        public SpecProcessor(string specsFile, string majorVersion, string minorVersion)
        {
            specs = new Specs(majorVersion, minorVersion);
            specs.Load(specsFile);
        }

        public void Process()
        {
            Console.WriteLine("Processing Specs...");
            Console.WriteLine($"BeginString: {specs.BeginString}");
            Console.WriteLine($"InMsgChecksum: {specs.InMsgChecksum}");
            Console.WriteLine($"OutMsgChecksum: {specs.OutMsgChecksum}");
            Console.WriteLine($"TimeZone: {specs.TimeZone}");

            specs.DisplayHeader();

            Console.WriteLine("\nMessages:");
            foreach (var msgEntry in specs.Messages)
            {
                var msg = msgEntry.Value;
                Console.WriteLine($"Message: Name={msg.Name}, MsgType={msg.MsgType}, Direction={msg.Direction}");
                Console.WriteLine($"  Children Count: {msg.Children.Count}");
                foreach (var childEntry in msg.Children)
                {
                    if (childEntry.Value is FieldDef fieldDef)
                    {
                        string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                            ? $", DateFormat={fieldDef.DateFormat}"
                            : "";
                        Console.WriteLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}, IsLog={fieldDef.IsLog}{dateFormatDisplay}");
                    }
                    else if (childEntry.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"  LoopDef: Tag={loopDef.Tag}, Name={loopDef.Name}, Counter={loopDef.Counter}, Children Count={loopDef.Children.Count}");
                        foreach (var nestedChildEntry in loopDef.Children)
                        {
                            if (nestedChildEntry.Value is FieldDef nestedField)
                            {
                                string nestedDateFormatDisplay = nestedField.Type == FieldType.Date && !string.IsNullOrEmpty(nestedField.DateFormat)
                                    ? $", DateFormat={nestedField.DateFormat}"
                                    : "";
                                Console.WriteLine($"    Nested FieldDef: Tag={nestedField.Tag}, Name={nestedField.Name}, Type={nestedField.Type}, DataField={nestedField.DataField}, IsLog={nestedField.IsLog}{nestedDateFormatDisplay}");
                            }
                        }
                    }
                }
            }
        }
    }
}