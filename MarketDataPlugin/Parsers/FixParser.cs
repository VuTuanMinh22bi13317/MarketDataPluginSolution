using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using MarketDataPlugin;
using MarketDataPlugin.DEF;
using System.Threading.Tasks;
using System.Linq;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;
        private readonly Dictionary<string, MsgDef> msgDefs = new Dictionary<string, MsgDef>();
        private readonly Dictionary<string, string> msgTableNames = new Dictionary<string, string>();
        private readonly string timeZone = "7";
        private readonly Dictionary<string, string> dateFormats = new Dictionary<string, string>();
        private readonly Msg msgParser;
        private readonly Dictionary<string, string> fieldNameToTag = new Dictionary<string, string>();

        public FixParser(Specs specs, string configPath)
        {
            this.specs = specs ?? throw new ArgumentNullException(nameof(specs));
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"Config file not found: {configPath}");
            (msgDefs, msgTableNames, timeZone, dateFormats) = LoadMsgDefs(configPath);
            msgParser = new Msg(specs, msgDefs, dateFormats);
        }

        private (Dictionary<string, MsgDef> msgDefs, Dictionary<string, string> msgTableNames, string timeZone, Dictionary<string, string> dateFormats) LoadMsgDefs(string configPath)
        {
            var loadedMsgDefs = new Dictionary<string, MsgDef>();
            var loadedMsgTableNames = new Dictionary<string, string>();
            var loadedDateFormats = new Dictionary<string, string>();
            string loadedTimeZone = "7";

            XmlDocument doc = new XmlDocument();
            doc.Load(configPath);
            XmlNodeList? msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes != null)
            {
                foreach (XmlNode node in msgNodes)
                {
                    string msgType = node.Attributes?["MsgType"]?.Value ?? "";
                    string name = node.Attributes?["Name"]?.Value ?? "";
                    string tableName = node.Attributes?["TableName"]?.Value ?? "";
                    loadedMsgTableNames[msgType] = tableName;
                    if (Enum.TryParse<DataDirection>(node.Attributes?["Direction"]?.Value ?? "InOut", out var direction))
                    {
                        var msgDef = new MsgDef(name, msgType, direction);
                        loadedMsgDefs[msgType] = msgDef;

                        var childNodes = node.SelectNodes("Field | Loop");
                        if (childNodes != null)
                        {
                            foreach (XmlNode childNode in childNodes)
                            {
                                if (childNode.Name == "Field")
                                {
                                    string tag = childNode.Attributes?["Tag"]?.Value ?? "";
                                    string fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                    fieldNameToTag[fieldName] = tag;
                                }
                            }

                            foreach (XmlNode childNode in childNodes)
                            {
                                if (childNode.Name == "Field")
                                {
                                    string tag = childNode.Attributes?["Tag"]?.Value ?? "";
                                    string fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                    string typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                    FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                    string? dataField = childNode.Attributes?["DataField"]?.Value ?? "";
                                    bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                    string? dateFormat = (type == FieldType.Date && childNode.Attributes?["DateFormat"] != null) ? childNode.Attributes?["DateFormat"]?.Value : null;

                                    var fieldDef = new FieldDef(tag, fieldName, type, dataField, false, dateFormat, isRequired);
                                    msgDef.AddChild(tag, fieldDef);
                                    if (dateFormat != null) loadedDateFormats[$"{msgType}:{tag}"] = dateFormat;
                                }
                                else if (childNode.Name == "Loop")
                                {
                                    string counterName = childNode.Attributes?["Counter"]?.Value ?? "";
                                    string counterTag = fieldNameToTag.TryGetValue(counterName, out var tag) ? tag : counterName;
                                    string loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                    var children = new Dictionary<string, FieldDef>();
                                    var loopDef = new LoopDef(counterTag, loopName, children);
                                    var loopChildNodes = childNode.SelectNodes("Field");
                                    if (loopChildNodes != null)
                                    {
                                        foreach (XmlNode loopChildNode in loopChildNodes)
                                        {
                                            string loopTag = loopChildNode.Attributes?["Tag"]?.Value ?? "";
                                            string loopFieldName = loopChildNode.Attributes?["Name"]?.Value ?? "Unknown";
                                            string loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                            FieldType loopType = Enum.TryParse(loopTypeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                            string? loopDataField = loopChildNode.Attributes?["DataField"]?.Value ?? "";
                                            bool loopIsRequired = bool.TryParse(loopChildNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                            string? loopDateFormat = (loopType == FieldType.Date && loopChildNode.Attributes?["DateFormat"] != null) ? loopChildNode.Attributes?["DateFormat"]?.Value : null;

                                            var loopFieldDef = new FieldDef(loopTag, loopFieldName, loopType, loopDataField, false, loopDateFormat, loopIsRequired);
                                            loopDef.AddChild(loopTag, loopFieldDef);
                                            if (loopDateFormat != null) loadedDateFormats[$"{msgType}:{loopTag}"] = loopDateFormat;
                                        }
                                    }
                                    msgDef.AddChild(counterTag, loopDef);
                                }
                            }
                        }
                    }
                }
            }

            return (loadedMsgDefs, loadedMsgTableNames, loadedTimeZone, loadedDateFormats);
        }

        public void DisplayConfig()
        {
            Console.WriteLine("FixParser Configuration:");
            Console.WriteLine($"InMsgChecksum: {specs.InMsgChecksum}");
            Console.WriteLine($"TimeZone: {timeZone} (Offset: +7 hours - Vietnam)");
            Console.WriteLine($"DelimiterChar: {(int)specs.DelimiterChar}");
            Console.WriteLine($"BeginString: {specs.BeginString}");

            Console.WriteLine("Header Fields:");
            foreach (var field in specs.HeaderFields)
            {
                var fieldDef = field.Value;
                if (fieldDef.Type == FieldType.Date)
                {
                    string dateFormat = !string.IsNullOrEmpty(fieldDef.DateFormat) ? fieldDef.DateFormat : "N/A";
                    Console.WriteLine($"  Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DateFormat: {dateFormat}");
                }
                else
                {
                    Console.WriteLine($"  Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}");
                }
            }

            Console.WriteLine("Message Definitions:");
            foreach (var msgDef in msgDefs)
            {
                var def = msgDef.Value;
                string tableName = msgTableNames.TryGetValue(msgDef.Key, out var name) ? name : "N/A";
                Console.WriteLine($"  MsgType: {msgDef.Key}, MsgName: {def.Name}, Direction: {def.Direction}, TableName: {tableName}");
                Console.WriteLine("  Fields:");
                foreach (var child in def.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        if (fieldDef.Type == FieldType.Date)
                        {
                            string dateFormat = !string.IsNullOrEmpty(fieldDef.DateFormat) ? fieldDef.DateFormat : "N/A";
                            Console.WriteLine($"    Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField ?? "N/A"}, DateFormat: {dateFormat}");
                        }
                        else
                        {
                            Console.WriteLine($"    Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField ?? "N/A"}");
                        }
                    }
                    else if (child.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"    Loop (Counter: {loopDef.Counter}, Name: {loopDef.Name}):");
                        foreach (var loopChild in loopDef.GetChildren())
                        {
                            if (loopChild.Value is FieldDef loopFieldDef)
                            {
                                string loopDateFormat = "N/A"; // Initialize with default value
                                if (loopFieldDef.Type == FieldType.Date)
                                {
                                    loopDateFormat = !string.IsNullOrEmpty(loopFieldDef.DateFormat) ? loopFieldDef.DateFormat : "N/A";
                                    Console.WriteLine($"      Tag: {loopFieldDef.Tag}, Name: {loopFieldDef.Name}, Type: {loopFieldDef.Type}, DataField: {loopFieldDef.DataField ?? "N/A"}, DateFormat: {loopDateFormat}");
                                }
                                else
                                {
                                    Console.WriteLine($"      Tag: {loopFieldDef.Tag}, Name: {loopFieldDef.Name}, Type: {loopFieldDef.Type}, DataField: {loopFieldDef.DataField ?? "N/A"}");
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<(string fileName, ParseResult result)> ParseFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                yield return ("", new ParseResult(new List<string> { $"Error: Directory not found - {folderPath}" }, null));
                yield break;
            }
            string[] filePaths = Directory.GetFiles(folderPath, "*.raw");
            Console.WriteLine($"Found {filePaths.Length} .raw files to process in {folderPath}");
            for (int i = 0; i < filePaths.Length; i++)
            {
                yield return (Path.GetFileName(filePaths[i]), ParseFile(filePaths[i]));
            }
        }

        public ParseResult ParseFile(string filePath)
        {
            var errors = new List<string>();
            var allSummaries = new List<MessageSummary>();
            var logBatch = new List<(int messageNumber, string logEntry)>();
            const int batchSize = 1000;

            try
            {
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");

                var messages = StreamMessages(filePath).ToList();
                int messageCount = messages.Count;
                Console.WriteLine($"Info: Found {messageCount} messages in file: {Path.GetFileName(filePath)}");

                var parseResults = new List<(int index, ParseResult result, string logEntry)>(messageCount);
                Parallel.ForEach(messages.Select((msg, idx) => (msg, idx)), message =>
                {
                    int idx = message.idx + 1;
                    if (idx % 1000 == 0 || idx == 1)
                        Console.WriteLine($"Debug: Processing message {idx} from {Path.GetFileName(filePath)}");

                    string logEntry = ParseAndLogMessage(message.msg, idx, errors);
                    var parseResult = msgParser.Parse(message.msg);

                    lock (parseResults)
                    {
                        parseResults.Add((idx, parseResult, logEntry));
                    }
                });

                parseResults.Sort((a, b) => a.index.CompareTo(b.index));

                foreach (var result in parseResults)
                {
                    if (!string.IsNullOrEmpty(result.logEntry))
                        logBatch.Add((result.index, result.logEntry));
                    if (result.result.Summary != null)
                        allSummaries.Add(result.result.Summary);
                    else
                        errors.Add($"Failed to parse message {result.index} from {Path.GetFileName(filePath)}");

                    if (logBatch.Count >= batchSize)
                    {
                        WriteLogBatch(logFilePath, logBatch);
                        logBatch.Clear();
                    }
                }

                if (logBatch.Count > 0)
                {
                    WriteLogBatch(logFilePath, logBatch);
                    logBatch.Clear();
                }

                Console.WriteLine($"Info: Processed {messageCount} messages from file: {Path.GetFileName(filePath)}");
                if (allSummaries.Count > 0)
                {
                    var representativeSummary = allSummaries[0];
                    representativeSummary.TotalFields = allSummaries.Sum(s => s.TotalFields);
                    return new ParseResult(errors, representativeSummary);
                }
                return new ParseResult(errors, null);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse file {Path.GetFileName(filePath)}: {ex.Message}");
                return new ParseResult(errors, null);
            }
        }

        private void WriteLogBatch(string logFilePath, List<(int messageNumber, string logEntry)> logBatch)
        {
            logBatch.Sort((a, b) => a.messageNumber.CompareTo(b.messageNumber));
            using (var logWriter = new StreamWriter(logFilePath, true))
            {
                foreach (var entry in logBatch)
                {
                    logWriter.Write(entry.logEntry);
                }
            }
        }

        private IEnumerable<string> StreamMessages(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string message = line.Trim();
                    if (!string.IsNullOrEmpty(message) && message.StartsWith($"8={specs.BeginString}" + specs.DelimiterChar))
                        yield return message;
                }
            }
        }

        private string ParseAndLogMessage(string message, int messageNumber, List<string> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Message {messageNumber} (Length: {message.Length}): {message.Replace((char)specs.DelimiterChar, ' ')}");

            string? msgType = msgParser.GetMsgType(message);
            if (string.IsNullOrEmpty(msgType))
            {
                errors.Add($"Failed to extract MsgType from message {messageNumber}");
                sb.AppendLine("Failed to extract MsgType");
            }
            else
            {
                sb.AppendLine($"Extracted MsgType: {msgType}");
            }

            return sb.ToString();
        }
    }
}