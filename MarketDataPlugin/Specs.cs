using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public static class Specs
    {
        #region Configuration Properties
        private static string _beginString = "FIX.4.4";
        private static char _delimiterChar = '\x01';
        private static bool _inMsgChecksum = true;
        private static Dictionary<string, MsgDef>? _msgDefs;

        public static string BeginString { get; set; } = _beginString;
        public static char DelimiterChar { get; set; } = _delimiterChar;
        public static bool InMsgChecksum { get; set; } = _inMsgChecksum;
        public static Dictionary<string, MsgDef>? MsgDefs { get => _msgDefs; set => _msgDefs = value; }
        #endregion

        #region Configuration Loading
        public static void LoadConfig(string configPath)
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"Config file not found: {configPath}");
            MsgDefs = LoadConfigInternal(configPath);
        }

        private static Dictionary<string, MsgDef> LoadConfigInternal(string configPath)
        {
            var msgDefs = new Dictionary<string, MsgDef>();
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(configPath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to load config file {configPath}: {ex.Message}", ex);
            }

            // Process Header
            XmlNode? headerNode = doc.SelectSingleNode("/Specs/Header");
            if (headerNode != null)
            {
                var headerDef = new MsgDef("Header", "Header", DataDirection.InOut)
                {
                    CheckSum = new FieldDef("10", "CheckSum")
                    {
                        Type = FieldType.Int,
                        IsRequired = true,
                        Tag = 10,
                        TimeZoneEffected = true,
                        Direction = DataDirection.InOut
                    }
                };

                var headerFields = headerNode.SelectNodes("Field");
                if (headerFields != null)
                {
                    foreach (XmlNode fieldNode in headerFields)
                    {
                        string? tagStr = fieldNode.Attributes?["Tag"]?.Value;
                        if (!string.IsNullOrEmpty(tagStr) && int.TryParse(tagStr, out int tag))
                        {
                            string? fieldName = fieldNode.Attributes?["Name"]?.Value ?? "Unknown";
                            string? typeStr = fieldNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                            FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                            string? dataField = fieldNode.Attributes?["DataField"]?.Value ?? string.Empty;
                            bool isRequired = bool.TryParse(fieldNode.Attributes?["IsRequired"]?.Value, out bool required) && required;
                            bool timeZoneEffected = bool.TryParse(fieldNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                            string? dateFormat = type == FieldType.Date ? fieldNode.Attributes?["DateFormat"]?.Value : string.Empty;

                            var fieldDef = new FieldDef(tagStr, fieldName)
                            {
                                Type = type,
                                DataField = dataField,
                                TimeZoneEffected = timeZoneEffected,
                                DateFormat = dateFormat,
                                Direction = DataDirection.InOut,
                                IsRequired = isRequired,
                                Tag = tag
                            };
                            headerDef.AddChild(tagStr, fieldDef);
                        }
                    }
                }
                msgDefs["Header"] = headerDef;
            }

            // Process Messages
            XmlNodeList? msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes == null)
            {
                throw new ArgumentException($"No <Msg> nodes found in config file {configPath}");
            }

            foreach (XmlNode node in msgNodes)
            {
                string? msgType = node.Attributes?["MsgType"]?.Value;
                if (string.IsNullOrEmpty(msgType)) throw new ArgumentException($"Missing MsgType attribute in Msg node");
                string? name = node.Attributes?["Name"]?.Value ?? "Unknown";
                if (Enum.TryParse<DataDirection>(node.Attributes?["Direction"]?.Value ?? "InOut", out var direction))
                {
                    var msgDef = new MsgDef(name, msgType, direction)
                    {
                        CheckSum = new FieldDef("10", "CheckSum")
                        {
                            Type = FieldType.Int,
                            IsRequired = true,
                            Tag = 10,
                            TimeZoneEffected = true,
                            Direction = DataDirection.InOut
                        }
                    };

                    var childNodes = node.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string? tagStr = childNode.Attributes?["Tag"]?.Value;
                                if (!string.IsNullOrEmpty(tagStr) && int.TryParse(tagStr, out int tag))
                                {
                                    string? fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                    string? typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                    FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                    string? dataField = childNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                    bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) && required;
                                    bool timeZoneEffected = bool.TryParse(childNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                    string? dateFormat = type == FieldType.Date ? childNode.Attributes?["DateFormat"]?.Value : string.Empty;

                                    var fieldDef = new FieldDef(tagStr, fieldName)
                                    {
                                        Type = type,
                                        DataField = dataField,
                                        TimeZoneEffected = timeZoneEffected,
                                        DateFormat = dateFormat,
                                        Direction = direction,
                                        IsRequired = isRequired,
                                        Tag = tag
                                    };
                                    msgDef.AddChild(tagStr, fieldDef);
                                }
                            }
                            else if (childNode.Name == "Loop")
                            {
                                string? counterName = childNode.Attributes?["Counter"]?.Value ?? string.Empty;
                                string? loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                var children = new Dictionary<string, FieldDef>();
                                var loopDef = new LoopDef(counterName, loopName, children);

                                var loopChildNodes = childNode.SelectNodes("Field");
                                if (loopChildNodes != null)
                                {
                                    foreach (XmlNode loopChildNode in loopChildNodes)
                                    {
                                        string? loopTagStr = loopChildNode.Attributes?["Tag"]?.Value;
                                        if (!string.IsNullOrEmpty(loopTagStr) && int.TryParse(loopTagStr, out int loopTag))
                                        {
                                            string? loopFieldName = loopChildNode.Attributes?["Name"]?.Value ?? "Unknown";
                                            string? loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                            FieldType loopType = Enum.TryParse(loopTypeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                            string? loopDataField = loopChildNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                            bool loopIsRequired = bool.TryParse(loopChildNode.Attributes?["IsRequired"]?.Value, out bool required) && required;
                                            bool loopTimeZoneEffected = bool.TryParse(loopChildNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                            string? loopDateFormat = loopType == FieldType.Date ? loopChildNode.Attributes?["DateFormat"]?.Value : string.Empty;

                                            var loopFieldDef = new FieldDef(loopTagStr, loopFieldName)
                                            {
                                                Type = loopType,
                                                DataField = loopDataField,
                                                TimeZoneEffected = loopTimeZoneEffected,
                                                DateFormat = loopDateFormat,
                                                Direction = direction,
                                                IsRequired = loopIsRequired,
                                                Tag = loopTag
                                            };
                                            loopDef.AddChild(loopTagStr, loopFieldDef);
                                        }
                                    }
                                }
                                msgDef.AddChild(counterName, loopDef);
                            }
                        }
                    }
                    msgDefs[msgType] = msgDef;
                }
            }
            return msgDefs;
        }
        #endregion

        #region Display Configuration
        public static void DisplayConfig()
        {
            if (MsgDefs == null)
            {
                Console.WriteLine("Configuration not loaded. Call LoadConfig first.");
                return;
            }
            Console.WriteLine("Specs Configuration:");
            Console.WriteLine($"InMsgChecksum: {InMsgChecksum}");
            Console.WriteLine($"DelimiterChar: {(int)DelimiterChar}");
            Console.WriteLine($"BeginString: {BeginString}");
            Console.WriteLine("Message Definitions:");
            foreach (var msgDef in MsgDefs)
            {
                Console.WriteLine($"  MsgType: {msgDef.Key}, Name: {msgDef.Value.Name}, Direction: {msgDef.Value.Direction}");
                Console.WriteLine("  Fields:");
                foreach (var child in msgDef.Value.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        string fieldInfo = $"    Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, IsRequired: {fieldDef.IsRequired}";
                        if (!string.IsNullOrEmpty(fieldDef.DataField)) fieldInfo += $", DataField: {fieldDef.DataField}";
                        if (fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat))
                            fieldInfo += $", DateFormat: {fieldDef.DateFormat}, TimeZoneEffected: {fieldDef.TimeZoneEffected}";
                        Console.WriteLine(fieldInfo);
                    }
                    else if (child.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"    Loop (Counter: {loopDef.Counter}, Name: {loopDef.Name}):");
                        foreach (var loopChild in loopDef.GetChildren())
                        {
                            var loopFieldDef = loopChild.Value;
                            string loopFieldInfo = $"      Tag: {loopFieldDef.Tag}, Name: {loopFieldDef.Name}, Type: {loopFieldDef.Type}, IsRequired: {loopFieldDef.IsRequired}";
                            if (!string.IsNullOrEmpty(loopFieldDef.DataField))
                                loopFieldInfo += $", DataField: {loopFieldDef.DataField}";
                            if (loopFieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(loopFieldDef.DateFormat))
                                loopFieldInfo += $", DateFormat: {loopFieldDef.DateFormat}, TimeZoneEffected: {loopFieldDef.TimeZoneEffected}";
                            Console.WriteLine(loopFieldInfo);
                        }
                    }
                }
            }
        }
        #endregion

        #region Message Processing
        public static IEnumerable<(string fileName, ParseResult result)> ParseFolder(string folderPath)
        {
            if (MsgDefs == null)
            {
                yield return ("", new ParseResult(new List<string> { "Error: Configuration not loaded. Call LoadConfig first." }, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0));
                yield break;
            }
            if (!Directory.Exists(folderPath))
            {
                yield return ("", new ParseResult(new List<string> { $"Error: Directory not found - {folderPath}" }, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0));
                yield break;
            }
            string[] filePaths = Directory.GetFiles(folderPath, "*.raw");
            Console.WriteLine($"Found {filePaths.Length} .raw files to process in {folderPath}");
            for (int i = 0; i < filePaths.Length; i++)
            {
                yield return (Path.GetFileName(filePaths[i]), ParseFile(filePaths[i]));
            }
        }

        public static ParseResult ParseFile(string filePath)
        {
            if (MsgDefs == null)
            {
                return new ParseResult(new List<string> { "Error: Configuration not loaded. Call LoadConfig first." }, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }
            var allErrors = new List<string>();
            var allFields = new List<MessageField>();
            var allLoops = new List<Loop>();
            var allUnparsedText = new StringBuilder();
            bool overallCheckSumPassed = true;
            int totalCalculatedChecksum = 0;

            try
            {
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");

                var rawData = File.ReadAllText(filePath);
                Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");

                var messages = MessageSplitter.SplitMessages(DelimiterChar, rawData, BeginString);
                Console.WriteLine($"Debug: Found {messages.Count} messages");
                var logBatch = new List<(int messageNumber, string logEntry)>();

                for (int i = 0; i < messages.Count; i++)
                {
                    var (message, _) = messages[i];
                    int messageNumber = i + 1;
                    Console.WriteLine($"Debug: Processing message {messageNumber} from {Path.GetFileName(filePath)}");

                    var messageErrors = new List<string>();
                    string? msgType = ExtractMsgType(message);
                    if (msgType != null && MsgDefs.TryGetValue(msgType, out MsgDef? msgDef))
                    {
                        var msg = new Msg(msgDef, message);
                        var parseResult = msg.Parse(message, messageErrors);

                        if (parseResult == null)
                        {
                            allErrors.AddRange(messageErrors.Select(e => $"Message {messageNumber}: {e}"));
                            logBatch.Add((messageNumber, $"Message {messageNumber} failed: {string.Join(", ", messageErrors)}"));
                        }
                        else
                        {
                            logBatch.Add((messageNumber, $"Message {messageNumber} parsed successfully"));
                            allErrors.AddRange(parseResult.Errors);
                            allFields.AddRange(parseResult.Fields);
                            allLoops.AddRange(parseResult.Loops);
                            if (!string.IsNullOrEmpty(parseResult.UnparsedText))
                            {
                                allUnparsedText.Append(parseResult.UnparsedText).Append(DelimiterChar);
                            }
                            overallCheckSumPassed &= parseResult.CheckSumPassed;
                            totalCalculatedChecksum += parseResult.CalculatedChecksum;
                        }
                    }
                    else
                    {
                        allErrors.Add($"Message {messageNumber}: Unknown MsgType or invalid message");
                        logBatch.Add((messageNumber, $"Message {messageNumber} failed: Unknown MsgType"));
                    }
                }

                if (logBatch.Count > 0)
                {
                    WriteLogBatch(logFilePath, logBatch);
                    logBatch.Clear();
                }

                string unparsedText = allUnparsedText.Length > 0 ? allUnparsedText.ToString().TrimEnd(DelimiterChar) : string.Empty;
                Console.WriteLine($"Debug: Total fields parsed for file: {allFields.Count}");
                return new ParseResult(allErrors, allFields, allLoops, unparsedText, overallCheckSumPassed, totalCalculatedChecksum);
            }
            catch (Exception ex)
            {
                allErrors.Add($"Failed to parse file: {Path.GetFileName(filePath)}: {ex.Message}");
                return new ParseResult(allErrors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }
        }

        private static void WriteLogBatch(string logFilePath, List<(int messageNumber, string logEntry)> logBatch)
        {
            logBatch.Sort((a, b) => a.messageNumber.CompareTo(b.messageNumber));
            using (var logWriter = new StreamWriter(logFilePath, true))
            {
                foreach (var entry in logBatch)
                {
                    logWriter.WriteLine($"{entry.logEntry}");
                }
            }
        }

        private static string? ExtractMsgType(string message)
        {
            string[] fieldPairs = message.Split(DelimiterChar);
            foreach (var pair in fieldPairs)
            {
                if (pair.StartsWith("35="))
                {
                    return pair.Substring(3);
                }
            }
            return null;
        }
        #endregion
    }
}