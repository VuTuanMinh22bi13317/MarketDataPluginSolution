using MarketDataPlugin;
using MarketDataPlugin.DEF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;

        public FixParser(string specsFile, string majorVersion, string minorVersion)
        {
            if (string.IsNullOrEmpty(specsFile) || !File.Exists(specsFile))
                throw new ArgumentException($"Invalid or missing XML file path: {specsFile}", nameof(specsFile));

            string xmlContent = File.ReadAllText(specsFile);
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new InvalidOperationException("XML content is empty.");

            var doc = LoadXmlDocument(xmlContent);
            var specsNode = doc.SelectSingleNode("/Specs");
            if (specsNode == null)
                throw new InvalidOperationException("Specs node not found in XML.");

            var headerFields = new Dictionary<string, FieldDef>();
            var headerNode = doc.SelectSingleNode("/Specs/Header");
            if (headerNode != null)
            {
                var childNodes = headerNode.SelectNodes("Field");
                if (childNodes != null)
                {
                    foreach (XmlNode childNode in childNodes)
                    {
                        var fieldDef = ParseFieldDef(childNode);
                        headerFields[fieldDef.Tag] = fieldDef;
                    }
                }
            }

            var messages = new Dictionary<string, MsgDef>();
            var msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes != null)
            {
                foreach (XmlNode msgNode in msgNodes)
                {
                    string name = msgNode.Attributes?["Name"]?.Value ?? "Unknown";
                    string msgType = msgNode.Attributes?["MsgType"]?.Value ?? "Unknown";
                    string directionStr = msgNode.Attributes?["Direction"]?.Value ?? "In";
                    DataDirection direction = directionStr.ToLower() switch
                    {
                        "in" => DataDirection.In,
                        "out" => DataDirection.Out,
                        "inout" => DataDirection.InOut,
                        _ => DataDirection.In
                    };

                    var msgDef = new MsgDef(name, msgType, direction);
                    messages[msgType] = msgDef;

                    var childNodes = msgNode.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                var fieldDef = ParseFieldDef(childNode);
                                msgDef.AddChild(fieldDef.Tag, fieldDef);
                            }
                            else if (childNode.Name == "Loop")
                            {
                                var loopDef = ParseLoopDef(childNode);
                                msgDef.AddChild(loopDef.Tag, loopDef);
                            }
                        }
                    }
                }
            }

            bool inMsgChecksum = bool.TryParse(specsNode?.Attributes?["InMsgChecksum"]?.Value, out bool inChecksum) && inChecksum;
            bool outMsgChecksum = bool.TryParse(specsNode?.Attributes?["OutMsgChecksum"]?.Value, out bool outChecksum) && outChecksum;
            int timeZone = int.TryParse(specsNode?.Attributes?["TimeZone"]?.Value, out int tz) ? tz : 7; // Default to +7 (Vietnam)

            specs = new Specs(majorVersion, minorVersion, headerFields.AsReadOnly(), inMsgChecksum, outMsgChecksum, timeZone, messages.AsReadOnly());
        }

        private XmlDocument LoadXmlDocument(string xmlContent)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);
                return doc;
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"Failed to parse XML: {ex.Message} at Line {ex.LineNumber}, Position {ex.LinePosition}", ex);
            }
        }

        private FieldDef ParseFieldDef(XmlNode node)
        {
            string fieldTagValue = node.Attributes?["Tag"]?.Value ?? "";
            string fieldName = node.Attributes?["Name"]?.Value ?? "Unknown";
            string typeStr = node.Attributes?["Type"]?.Value?.Trim() ?? "String";
            FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;

            string? dataField = node.Attributes?["DataField"]?.Value
                             ?? node.Attributes?["dataField"]?.Value
                             ?? node.Attributes?["DATAFIELD"]?.Value
                             ?? string.Empty;

            bool isLog = bool.TryParse(node.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
            string? dateFormat = (type == FieldType.Date && node.Attributes?["DateFormat"] != null) ? node.Attributes?["DateFormat"]?.Value : null;
            bool isRequired = bool.TryParse(node.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;

            return new FieldDef(fieldTagValue, fieldName, type, dataField, isLog, dateFormat, isRequired);
        }

        private LoopDef ParseLoopDef(XmlNode node)
        {
            string loopTagValue = node.Attributes?["Tag"]?.Value ?? "";
            string loopName = node.Attributes?["Name"]?.Value ?? "Unknown";
            string counterValue = node.Attributes?["Counter"]?.Value ?? "";

            var loopDef = new LoopDef(loopTagValue, loopName, counterValue);

            var loopChildNodes = node.SelectNodes("Field");
            if (loopChildNodes != null)
            {
                foreach (XmlNode loopChildNode in loopChildNodes)
                {
                    var loopFieldDef = ParseFieldDef(loopChildNode);
                    loopDef.AddChild(loopFieldDef.Tag, loopFieldDef);
                }
            }

            return loopDef;
        }

        public void ParseFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Error: Folder '{folderPath}' does not exist.");
                return;
            }

            var files = Directory.GetFiles(folderPath, "*.raw", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine($"Error: No .raw files found in folder '{folderPath}'.");
                return;
            }

            var plugin = new Plugin(specs); // Instantiate Plugin here with the loaded specs
            foreach (var file in files)
            {
                Console.WriteLine($"\nProcessing file: {Path.GetFileName(file)}");
                int messageCount = 0;

                try
                {
                    string fileContent = File.ReadAllText(file) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        Console.WriteLine($"Warning: File {Path.GetFileName(file)} is empty.");
                        continue;
                    }

                    foreach (var result in plugin.ProcessRawData(fileContent))
                    {
                        messageCount++;
                        Console.WriteLine($"\nMessage {messageCount}:");
                        if (result.MessageSummary != null)
                        {
                            Console.WriteLine($"  MsgType: {result.MessageSummary.MsgType}, MsgName: {result.MessageSummary.MsgName}, Total Fields: {result.MessageSummary.TotalFields}");
                            if (result.Errors.Any())
                            {
                                Console.WriteLine($"  Errors: {string.Join(", ", result.Errors)}");
                            }

                            // Hiển thị header fields
                            if (result.MessageFields != null)
                            {
                                Console.WriteLine("  Header Fields:");
                                var headerTags = new HashSet<string> { "8", "9", "35", "49", "56", "34", "52", "10" };
                                foreach (var field in result.MessageFields)
                                {
                                    if (field.Tag != null && headerTags.Contains(field.Tag))
                                    {
                                        Console.WriteLine($"    {field.Tag}: {field.Name} = {field.Value}");
                                    }
                                }

                                // Hiển thị body fields
                                Console.WriteLine("  Body Fields:");
                                foreach (var field in result.MessageFields)
                                {
                                    if (field.Tag != null && !headerTags.Contains(field.Tag))
                                    {
                                        Console.WriteLine($"    {field.Tag}: {field.Name} = {field.Value}");
                                    }
                                }
                            }

                            if (result.Loops != null)
                            {
                                int loopIndex = 0;
                                foreach (var loop in result.Loops)
                                {
                                    Console.WriteLine($"  Loop {loopIndex}:");
                                    foreach (var field in loop)
                                    {
                                        Console.WriteLine($"    {field.Tag}: {field.Name} = {field.Value}");
                                    }
                                    loopIndex++;
                                }
                            }
                        }
                        else if (result.Errors.Any())
                        {
                            Console.WriteLine($"  Skipped - Errors: {string.Join(", ", result.Errors)}");
                        }
                    }

                    if (messageCount == 0)
                    {
                        Console.WriteLine($"Warning: No valid messages found in file {Path.GetFileName(file)}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }
    }

    public class ParseResult
    {
        public List<string> Errors { get; } = new List<string>();
        public MessageSummary? MessageSummary { get; set; }
        public List<FieldOutput> MessageFields { get; set; } = new List<FieldOutput>();
        public List<List<FieldOutput>> Loops { get; set; } = new List<List<FieldOutput>>();

        public bool Success => !HasCriticalErrors;
        public bool HasErrors => Errors.Any();
        public bool HasCriticalErrors => Errors.Any(e => e.StartsWith("Error:"));

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }

    public class MessageSummary
    {
        public string? MsgType { get; set; }
        public string? MsgName { get; set; }
        public int TotalFields { get; set; }
        public List<FieldOutput>? MessageFields { get; set; }
        public List<List<FieldOutput>>? Loops { get; set; }
    }

    public class FieldOutput
    {
        public string? Tag { get; set; }
        public string? Name { get; set; }
        public object? Value { get; set; } // Changed to object to handle different types
        public string? Type { get; set; }
        public string? DataField { get; set; }
        public bool? IsLog { get; set; }
    }
}