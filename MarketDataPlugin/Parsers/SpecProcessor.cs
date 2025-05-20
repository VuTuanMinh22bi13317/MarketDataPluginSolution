using System;
using System.IO;

namespace MarketDataPlugin
{
    public class SpecProcessor
    {
        public static void ProcessSpecs(string xmlFilePath)
        {
            Console.WriteLine("--- Starting MarketDataPlugin Test ---");

            Console.WriteLine("\nTest 1: Parsing FIX Specification (FixParser -> Specs)");
            if (!File.Exists(xmlFilePath))
            {
                Console.WriteLine($"Error: XML file not found at {xmlFilePath}. Please create it.");
                return;
            }

            var specs = new Specs("4", "4");
            try
            {
                FixParser.ParseSpecsFromXml(xmlFilePath, specs);
                Console.WriteLine("Specs parsed successfully:");
                Console.WriteLine($"  BeginString: {specs.BeginString ?? "Not specified"}");
                Console.WriteLine($"  InMsgChecksum: {specs.InMsgChecksum}");
                Console.WriteLine($"  OutMsgChecksum: {specs.OutMsgChecksum}");
                Console.WriteLine($"  TimeZone: {specs.TimeZone}");
                Console.WriteLine($"  Messages Count: {specs.Messages.Count}");
                Console.WriteLine($"  Loops Count: {specs.Loops.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return;
            }

            Console.WriteLine("\nTest 2: Validating Message Definitions (MsgDef, FieldDef, LoopDef)");
            if (specs.Messages.Count == 0)
            {
                Console.WriteLine("Error: No messages defined in specs.");
                return;
            }

            foreach (var message in specs.Messages)
            {
                if (message is MsgDef msgDef)
                {
                    Console.WriteLine($"Message: Name={msgDef.Name}, MsgType={msgDef.MsgType}, Direction={msgDef.Direction}");
                    Console.WriteLine($"  Children Count: {msgDef.Children.Count}");
                    foreach (var child in msgDef.Children)
                    {
                        if (child is FieldDef fieldDef)
                        {
                            string dateFormatStr = fieldDef.Type == FieldType.Date && fieldDef.DateFormat != null
                                ? $", DateFormat={fieldDef.DateFormat}"
                                : "";
                            Console.WriteLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, Required={fieldDef.Required}, DataField={fieldDef.DataField ?? "N/A"}, IsLog={fieldDef.IsLog}{dateFormatStr}");
                        }
                        else if (child is LoopDef loopDef)
                        {
                            Console.WriteLine($"  LoopDef: Tag={loopDef.Tag}, Name={loopDef.Name}, Counter={loopDef.Counter}, Required={loopDef.Required}, Children Count={loopDef.Children.Count}");
                            foreach (var loopChild in loopDef.Children)
                            {
                                if (loopChild is FieldDef loopFieldDef)
                                {
                                    string loopDateFormatStr = loopFieldDef.Type == FieldType.Date && loopFieldDef.DateFormat != null
                                        ? $", DateFormat={loopFieldDef.DateFormat}"
                                        : "";
                                    Console.WriteLine($"    Nested FieldDef: Tag={loopFieldDef.Tag}, Name={loopFieldDef.Name}, Type={loopFieldDef.Type}, Required={loopFieldDef.Required}, DataField={loopFieldDef.DataField ?? "N/A"}, IsLog={loopFieldDef.IsLog}{loopDateFormatStr}");
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine("\n--- Test Completed (Config Display Only) ---");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}