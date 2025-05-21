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
            foreach (var msgDef in specs.GetAllMessages())
            {
                Console.WriteLine($"Message: Name={msgDef.Name}, MsgType={msgDef.MsgType}, Direction={msgDef.Direction}");
                Console.WriteLine($"  Children Count: {msgDef.GetChildren().Count}");
                foreach (var childEntry in msgDef.GetChildren())
                {
                    if (childEntry.Value is FieldDef fieldDef)
                    {
                        string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                            ? $", DateFormat={fieldDef.DateFormat}"
                            : "";
                        string isLogDisplay = (fieldDef.Tag == "8" || fieldDef.Tag == "9" || fieldDef.Tag == "35" || fieldDef.Tag == "49" ||
                                              fieldDef.Tag == "56" || fieldDef.Tag == "34" || fieldDef.Tag == "52") ? $", IsLog={fieldDef.IsLog}" : "";
                        Console.WriteLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}{dateFormatDisplay}{isLogDisplay}");
                    }
                    else if (childEntry.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"  LoopDef: Tag={loopDef.Tag}, Name={loopDef.Name}, Counter={loopDef.Counter}, Children Count={loopDef.GetChildren().Count}");
                        foreach (var nestedChildEntry in loopDef.GetChildren())
                        {
                            if (nestedChildEntry.Value is FieldDef nestedField)
                            {
                                string nestedDateFormatDisplay = nestedField.Type == FieldType.Date && !string.IsNullOrEmpty(nestedField.DateFormat)
                                    ? $", DateFormat={nestedField.DateFormat}"
                                    : "";
                                Console.WriteLine($"    Nested FieldDef: Tag={nestedField.Tag}, Name={nestedField.Name}, Type={nestedField.Type}, DataField={nestedField.DataField}{nestedDateFormatDisplay}");
                            }
                        }
                    }
                }
            }
        }
    }
}