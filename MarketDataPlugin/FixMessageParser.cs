using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using MarketDataPlugin.DEF;
using System.Linq;
using System.Globalization;

namespace MarketDataPlugin
{
    public class FixMessageParser
    {
        // Configuration fields with public properties
        private readonly string _beginString = "FIX.4.4";
        private readonly char _delimiterChar = '\x01';
        private readonly bool _inMsgChecksum = true;
        private readonly Dictionary<string, FieldDef> _headerFields = new Dictionary<string, FieldDef>();
        private readonly Dictionary<string, MsgDef> _msgDefs = new Dictionary<string, MsgDef>();
        private readonly Dictionary<string, string> _dateFormats = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _msgTableNames = new Dictionary<string, string>();
        private readonly string _timeZone = "7";
        private readonly Dictionary<string, string> _fieldNameToTag = new Dictionary<string, string>();

        public string BeginString => _beginString;
        public char DelimiterChar => _delimiterChar;
        public bool InMsgChecksum => _inMsgChecksum;
        public Dictionary<string, FieldDef> HeaderFields => _headerFields;
        public Dictionary<string, MsgDef> MsgDefs => _msgDefs;
        public Dictionary<string, string> DateFormats => _dateFormats;

        private readonly Msg msgParser;

        // Checksum method as public
        public static int CalculateCheckSum(byte[] bytes)
        {
            int cks = 0;
            foreach (byte b in bytes)
                cks += b;
            return cks % 256;
        }

        public FixMessageParser(string configPath)
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"Config file not found: {configPath}");
            LoadConfig(configPath);
            msgParser = new Msg(this);
        }

        private void LoadConfig(string configPath)
        {
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(configPath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to load config file {configPath}: {ex.Message}", ex);
            }

            // Load header fields
            XmlNodeList? headerNodes = doc.SelectNodes("/Specs/Header/Field");
            if (headerNodes == null)
            {
                throw new ArgumentException($"No <Header> nodes found in config file {configPath}");
            }
            foreach (XmlNode node in headerNodes)
            {
                string? tagStr = node.Attributes?["Tag"]?.Value;
                if (string.IsNullOrEmpty(tagStr)) throw new ArgumentException($"Missing Tag attribute in Header Field node");
                if (!int.TryParse(tagStr, out int tag))
                    throw new ArgumentException($"Invalid Tag value '{tagStr}' in Header Field node");
                string? name = node.Attributes?["Name"]?.Value;
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException($"Name attribute cannot be empty in Header Field node with Tag '{tagStr}'");
                string? typeStr = node.Attributes?["Type"]?.Value?.Trim() ?? "String";
                FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                string? dataField = node.Attributes?["DataField"]?.Value ?? string.Empty;
                bool timeZoneEffected = bool.TryParse(node.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                string? dateFormat = type == FieldType.Date && node.Attributes?["DateFormat"]?.Value != null ? node.Attributes["DateFormat"].Value : null;

                var fieldDef = new FieldDef(tagStr, name)
                {
                    Type = type,
                    DataField = dataField,
                    TimeZoneEffected = timeZoneEffected,
                    DateFormat = dateFormat,
                    Tag = tag
                };
                _headerFields[tagStr] = fieldDef;
            }

            // Load message definitions
            XmlNodeList? msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes == null)
            {
                throw new ArgumentException($"No <Msg> nodes found in config file {configPath}");
            }
            foreach (XmlNode node in msgNodes)
            {
                string? msgType = node.Attributes?["MsgType"]?.Value;
                if (string.IsNullOrEmpty(msgType)) throw new ArgumentException($"Missing MsgType attribute in Msg node");
                string? name = node.Attributes?["Name"]?.Value;
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException($"Name attribute cannot be empty in Msg node with MsgType '{msgType}'");
                string? tableName = node.Attributes?["TableName"]?.Value ?? string.Empty;
                _msgTableNames[msgType] = tableName;
                if (Enum.TryParse<DataDirection>(node.Attributes?["Direction"]?.Value ?? "InOut", out var direction))
                {
                    var msgDef = new MsgDef(name, msgType, direction);
                    _msgDefs[msgType] = msgDef;

                    var childNodes = node.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        _fieldNameToTag.Clear();
                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string? tag = childNode.Attributes?["Tag"]?.Value ?? string.Empty;
                                string? fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                _fieldNameToTag[fieldName] = tag;
                            }
                        }

                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string? tagStr = childNode.Attributes?["Tag"]?.Value;
                                if (string.IsNullOrEmpty(tagStr)) throw new ArgumentException($"Missing Tag attribute in Field node");
                                if (!int.TryParse(tagStr, out int tag))
                                    throw new ArgumentException($"Invalid Tag value '{tagStr}' in Field node");
                                string? fieldName = childNode.Attributes?["Name"]?.Value;
                                if (string.IsNullOrWhiteSpace(fieldName))
                                    throw new ArgumentException($"Name attribute cannot be empty in Field node with tag '{tagStr}' in MsgType '{msgType}'");
                                string? typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                string? dataField = childNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                bool timeZoneEffected = bool.TryParse(childNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                string? dateFormat = type == FieldType.Date && childNode.Attributes?["DateFormat"]?.Value != null ? childNode.Attributes["DateFormat"].Value : null;

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
                                if (type == FieldType.Date && !string.IsNullOrEmpty(dateFormat))
                                    _dateFormats[$"{msgType}:{tagStr}"] = dateFormat;
                            }
                            else if (childNode.Name == "Loop")
                            {
                                string? counterName = childNode.Attributes?["Counter"]?.Value ?? string.Empty;
                                string counterTag = _fieldNameToTag.TryGetValue(counterName, out var tag) ? tag : counterName;
                                string? loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                var children = new Dictionary<string, FieldDef>();
                                var loopDef = new LoopDef(counterTag, loopName, children);
                                var loopChildNodes = childNode.SelectNodes("Field");
                                if (loopChildNodes != null)
                                {
                                    foreach (XmlNode loopChildNode in loopChildNodes)
                                    {
                                        string? loopTagStr = loopChildNode.Attributes?["Tag"]?.Value;
                                        if (string.IsNullOrEmpty(loopTagStr)) throw new ArgumentException($"Missing Tag attribute in Loop Field node");
                                        if (!int.TryParse(loopTagStr, out int loopTag))
                                            throw new ArgumentException($"Invalid Tag value '{loopTagStr}' in Loop Field node");
                                        string? loopFieldName = loopChildNode.Attributes?["Name"]?.Value;
                                        if (string.IsNullOrWhiteSpace(loopFieldName))
                                            throw new ArgumentException($"Name attribute cannot be empty in Loop Field node with tag '{loopTagStr}' in MsgType '{msgType}'");
                                        string? loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                        FieldType loopType = Enum.TryParse(loopTypeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                        string? loopDataField = loopChildNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                        bool loopIsRequired = bool.TryParse(loopChildNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                        bool loopTimeZoneEffected = bool.TryParse(loopChildNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                        string? loopDateFormat = loopType == FieldType.Date && loopChildNode.Attributes?["DateFormat"]?.Value != null ? loopChildNode.Attributes["DateFormat"].Value : null;

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
                                        if (loopType == FieldType.Date && !string.IsNullOrEmpty(loopDateFormat))
                                            _dateFormats[$"{msgType}:{loopTagStr}"] = loopDateFormat;
                                    }
                                }
                                msgDef.AddChild(counterTag, loopDef);
                            }
                        }
                    }
                }
            }
        }

        public void DisplayConfig()
        {
            Console.WriteLine("FixMessageParser Configuration:");
            Console.WriteLine($"InMsgChecksum: {_inMsgChecksum}");
            Console.WriteLine($"TimeZone: {_timeZone} (Offset: +7 hours - Vietnam)");
            Console.WriteLine($"DelimiterChar: {(int)_delimiterChar}");
            Console.WriteLine($"BeginString: {_beginString}");

            Console.WriteLine("Header Fields:");
            foreach (var field in _headerFields)
            {
                var fieldDef = field.Value;
                if (fieldDef.Type == FieldType.Date)
                {
                    Console.WriteLine($"  Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DateFormat: {fieldDef.DateFormat}, TimeZoneEffected: {fieldDef.TimeZoneEffected}");
                }
                else
                {
                    Console.WriteLine($"  Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}");
                }
            }

            Console.WriteLine("Message Definitions:");
            foreach (var msgDef in _msgDefs)
            {
                var def = msgDef.Value;
                string tableName = _msgTableNames.TryGetValue(msgDef.Key, out var name) ? name : "N/A";
                Console.WriteLine($"  MsgType: {msgDef.Key}, MsgName: {def.Name}, Direction: {def.Direction}, TableName: {tableName}");
                Console.WriteLine("  Fields:");
                foreach (var child in def.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        if (fieldDef.Type == FieldType.Date)
                        {
                            Console.WriteLine($"    Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField}, DateFormat: {fieldDef.DateFormat}, TimeZoneEffected: {fieldDef.TimeZoneEffected}");
                        }
                        else
                        {
                            Console.WriteLine($"    Tag: {fieldDef.Tag}, Name: {fieldDef.Name}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField}");
                        }
                    }
                    else if (child.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"    Loop (Counter: {loopDef.Counter}, Name: {loopDef.Name}):");
                        foreach (var loopChild in loopDef.GetChildren())
                        {
                            var loopFieldDef = loopChild.Value;
                            if (loopFieldDef.Type == FieldType.Date)
                            {
                                Console.WriteLine($"      Tag: {loopFieldDef.Tag}, Name: {loopFieldDef.Name}, Type: {loopFieldDef.Type}, DataField: {loopFieldDef.DataField}, DateFormat: {loopFieldDef.DateFormat}, TimeZoneEffected: {loopFieldDef.TimeZoneEffected}");
                            }
                            else
                            {
                                Console.WriteLine($"      Tag: {loopFieldDef.Tag}, Name: {loopFieldDef.Name}, Type: {loopFieldDef.Type}, DataField: {loopFieldDef.DataField}");
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
                yield return ("", new ParseResult(new List<string> { $"Error: Directory not found - {folderPath}" }, new List<MessageField>(), new List<Loop>(), string.Empty, checkSumPassed: true, calculatedChecksum: 0));
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
            var logBatch = new List<(int messageNumber, string logEntry)>();

            try
            {
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");

                var rawData = File.ReadAllText(filePath);
                Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");

                var messages = MessageSplitter.SplitMessages(DelimiterChar, rawData, BeginString);
                for (int i = 0; i < messages.Count; i++)
                {
                    var (message, _) = messages[i]; // Rút trích message từ tuple, bỏ qua totalLength
                    int messageNumber = i + 1;
                    Console.WriteLine($"Debug: Processing message {messageNumber} from {Path.GetFileName(filePath)}");

                    var messageErrors = new List<string>();
                    var parseResult = msgParser.Parse(message, messageErrors);

                    if (parseResult == null)
                    {
                        errors.AddRange(messageErrors.Select(e => $"Message {messageNumber}: {e}"));
                        string logEntry = ParseAndLogMessage(this, message, messageNumber, messageErrors, null, InMsgChecksum);
                        logBatch.Add((messageNumber, logEntry));
                        continue;
                    }

                    if (messageErrors.Any())
                    {
                        errors.AddRange(messageErrors.Select(e => $"Message {messageNumber}: {e}"));
                    }

                    string logEntrySuccess = ParseAndLogMessage(this, message, messageNumber, messageErrors, parseResult, InMsgChecksum);
                    logBatch.Add((messageNumber, logEntrySuccess));
                }

                // Tiếp tục xử lý logBatch như cũ
                if (logBatch.Count > 0)
                {
                    WriteLogBatch(logFilePath, logBatch);
                    logBatch.Clear();
                }

                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse file: {Path.GetFileName(filePath)}: {ex.Message}");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
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

        private string ParseAndLogMessage(FixMessageParser parser, string message, int messageNumber, List<string> errors, ParseResult? parseResult, bool inMsgChecksum)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- Message {messageNumber} ---");
            sb.AppendLine($"Raw Length: {message.Length}");

            if (parseResult != null && inMsgChecksum)
            {
                sb.AppendLine($"Checksum Status: {parseResult.CheckSumPassed}");
                sb.AppendLine($"Calculated Checksum: {parseResult.CalculatedChecksum}");
                int trailerIdx = message.LastIndexOf($"{parser.DelimiterChar}10=");
                if (trailerIdx >= 0)
                {
                    int checksumEnd = message.IndexOf(parser.DelimiterChar, trailerIdx + 4);
                    string expectedCheckSum = message.Substring(trailerIdx + 4, Math.Min(3, checksumEnd - (trailerIdx + 4))).TrimEnd(parser.DelimiterChar).PadLeft(3, '0');
                    sb.AppendLine($"Expected Checksum: {expectedCheckSum}");
                }
                string bodyForChecksum = trailerIdx >= 0 ? message.Substring(0, trailerIdx) : message; // Chỉ lấy đến trước tag 10
                byte[] bodyBytes = Encoding.ASCII.GetBytes(bodyForChecksum);
                int calculated = CalculateCheckSum(bodyBytes);
                sb.AppendLine($"Checksum Body Length: {bodyBytes.Length}");
                sb.AppendLine($"Checksum Body Bytes: {BitConverter.ToString(bodyBytes)}");
            }

            if (errors?.Count > 0)
            {
                sb.AppendLine("Parsing Errors:");
                foreach (var error in errors)
                {
                    sb.AppendLine($"  - {error}");
                }
            }

            if (parseResult == null)
            {
                sb.AppendLine("--- End Message ---");
                sb.AppendLine();
                return sb.ToString();
            }

            string msgType = parseResult.Fields?.FirstOrDefault(f => f.Tag == "35")?.Value ?? "Unknown";
            sb.AppendLine($"MsgType: {msgType}");

            sb.AppendLine("Tags in Message:");
            string[] fieldPairs = message.Split(parser.DelimiterChar);
            foreach (var pair in fieldPairs)
            {
                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);
                    string fieldName = parseResult.Fields?.FirstOrDefault(f => f.Tag == tag)?.Name ?? "Unknown";
                    sb.AppendLine($"  - Tag: {tag}, Name: {fieldName}, Value: {value}");
                }
            }

            if (parseResult.Fields != null && parseResult.Fields.Any())
            {
                sb.AppendLine("Parsed Fields:");
                foreach (var field in parseResult.Fields)
                {
                    sb.AppendLine($"  - Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                }
            }

            if (parseResult.Loops != null && parseResult.Loops.Any())
            {
                sb.AppendLine("Parsed Loops:");
                for (int i = 0; i < parseResult.Loops.Count; i++)
                {
                    var loop = parseResult.Loops[i];
                    sb.AppendLine($"  - Loop {i + 1} (Counter: {loop.Counter}):");
                    foreach (var field in loop.ParsedFields)
                    {
                        sb.AppendLine($"    - Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(parseResult.UnparsedText))
            {
                sb.AppendLine("Unparsed Fields: " + parseResult.UnparsedText);
            }

            sb.AppendLine("--- End Message ---");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}