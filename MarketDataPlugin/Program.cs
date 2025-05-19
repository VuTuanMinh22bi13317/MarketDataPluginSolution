using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MarketDataPlugin;

namespace MarketDataPlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- Starting MarketDataPlugin Test ---");

            // Step 1: Test FixParser and Specs Parsing
            Console.WriteLine("\nTest 1: Parsing FIX Specification (FixParser -> Specs)");
            string xmlFilePath = "C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
            if (!File.Exists(xmlFilePath))
            {
                Console.WriteLine($"Error: XML file not found at {xmlFilePath}. Please create it.");
                Console.ReadKey();
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
                Console.ReadKey();
                return;
            }

            // Step 2: Validate MsgDef, FieldDef, and LoopDef
            Console.WriteLine("\nTest 2: Validating Message Definitions (MsgDef, FieldDef, LoopDef)");
            MsgDef? message = null;
            if (specs.Messages.Count == 0)
            {
                Console.WriteLine("Error: No messages defined in specs. Exiting.");
                Console.ReadKey();
                return;
            }

            message = specs.Messages[0] as MsgDef;
            if (message == null)
            {
                Console.WriteLine("Error: First message is not a valid MsgDef. Exiting.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"First Message: Name={message.Name}, MsgType={message.MsgType}, Direction={message.Direction}");
            Console.WriteLine($"Children Count: {message.Children.Count}");
            foreach (var child in message.Children)
            {
                if (child is FieldDef fieldDef)
                {
                    Console.WriteLine($"  FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, Required={fieldDef.Required}");
                }
                else if (child is LoopDef loopDef)
                {
                    Console.WriteLine($"  LoopDef: Name={loopDef.Name}, Tag={loopDef.Tag}, Counter={loopDef.Counter}, Required={loopDef.Required}, Children={loopDef.Children.Count}");
                    foreach (var loopChild in loopDef.Children)
                    {
                        if (loopChild is FieldDef loopFieldDef)
                        {
                            Console.WriteLine($"    Nested FieldDef: Tag={loopFieldDef.Tag}, Name={loopFieldDef.Name}, Type={loopFieldDef.Type}, Required={loopFieldDef.Required}");
                        }
                    }
                }
            }

            // Step 3: Test Runtime Msg, Field, and Loop with FIX Messages
            Console.WriteLine("\nTest 3: Processing FIX Messages (Msg, Field, Loop)");
            string messageFolder = @"C:/Users/Admin/Downloads/20250416 (1)/20250416";
            if (!Directory.Exists(messageFolder))
            {
                Console.WriteLine($"Error: Messages folder not found at {messageFolder}. Please create it and add raw FIX files.");
                Console.ReadKey();
                return;
            }

            string[] messageFiles = Directory.GetFiles(messageFolder, "*.txt");
            if (messageFiles.Length == 0)
            {
                Console.WriteLine($"Warning: No .txt files found in {messageFolder}. Skipping message processing.");
            }
            else
            {
                foreach (string filePath in messageFiles)
                {
                    Console.WriteLine($"\nProcessing File: {Path.GetFileName(filePath)}");
                    string fixMessage = File.ReadAllText(filePath).Replace("|", "\x01");

                    // Create runtime Msg instance
                    var runtimeMsg = new Msg(message.Name, message.MsgType);
                    foreach (var child in message.Children)
                    {
                        if (child is FieldDef fieldDef)
                        {
                            runtimeMsg.AddChild(new Field(fieldDef.Tag, fieldDef.Name, fieldDef.Type, fieldDef.Required));
                        }
                        else if (child is LoopDef loopDef)
                        {
                            runtimeMsg.AddChild(new Loop(loopDef.Name, loopDef));
                        }
                    }

                    // Parse the FIX message
                    try
                    {
                        runtimeMsg.Parse(fixMessage);
                        Console.WriteLine("Parsed Successfully:");
                        Console.WriteLine($"  Message: Name={runtimeMsg.Name}, MsgType={runtimeMsg.MsgType}");
                        Console.WriteLine($"  Children Count: {runtimeMsg.Children.Count}");
                        foreach (var child in runtimeMsg.Children)
                        {
                            if (child is Field field)
                            {
                                Console.WriteLine($"    Field: Tag={field.Tag}, Name={field.Name}, Value={field.ToString()}, Raw={field.Raw}");
                            }
                            else if (child is Loop loop)
                            {
                                Console.WriteLine($"    Loop: Name={loop.Name}, Children Count={loop.Children.Count}");
                                foreach (var loopField in loop.GetAllFields())
                                {
                                    Console.WriteLine($"      Loop Field: Tag={loopField.Tag}, Name={loopField.Name}, Value={loopField.ToString()}, Raw={loopField.Raw}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing FIX message: {ex.Message}");
                    }
                }
            }

            // Step 4: Test Edge Cases
            Console.WriteLine("\nTest 4: Edge Cases");
            // Test with empty FIX message
            var emptyMsg = new Msg(message.Name, message.MsgType);
            foreach (var child in message.Children)
            {
                if (child is FieldDef fieldDef)
                {
                    emptyMsg.AddChild(new Field(fieldDef.Tag, fieldDef.Name, fieldDef.Type, fieldDef.Required));
                }
            }
            emptyMsg.Parse("");
            Console.WriteLine("Empty Message Parsed:");
            Console.WriteLine($"  Children Count: {emptyMsg.Children.Count}");
            foreach (var child in emptyMsg.Children)
            {
                if (child is Field field)
                {
                    Console.WriteLine($"    Field: Tag={field.Tag}, Name={field.Name}, Value={field.ToString()} (should be empty)");
                }
            }

            Console.WriteLine("\n--- Test Completed ---");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}