using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MarketDataPlugin; // For Specs
using MarketDataPlugin.DEF; // For MsgDef, Msg, FieldDef, etc.
using System.Text; // For Encoding
using System.Globalization; // For date parsing with format

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;
        private readonly Dictionary<string, MsgDef> msgDefs;
        private readonly Dictionary<string, string> msgTableNames; // Store TableName per MsgType
        private readonly string timeZone; // Store timezone locally since Specs.TimeZone is read-only
        private readonly Dictionary<string, string> dateFormats; // Map Tag to DateFormat for Date fields

        public FixParser(Specs specs, string configPath)
        {
            this.specs = specs ?? throw new ArgumentNullException(nameof(specs));
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"Config file not found: {configPath}");
            Console.WriteLine($"Debug: Loading message definitions from {configPath}");
            (this.msgDefs, this.msgTableNames, this.timeZone, this.dateFormats) = LoadMsgDefs(configPath);
        }

        public void DisplayConfig()
        {
            Console.WriteLine("FixParser Configuration:");
            Console.WriteLine($"InMsgChecksum: {specs.InMsgChecksum}");
            Console.WriteLine($"TimeZone: {timeZone} (Offset: {GetTimeZoneOffset(timeZone).ToString()} hours)");
            Console.WriteLine($"DelimiterChar: {(int)specs.DelimiterChar}");
            Console.WriteLine($"BeginString: {specs.BeginString}");

            Console.WriteLine("Header Fields:");
            var headerFields = specs.GetType().GetProperty("HeaderFields")?.GetValue(specs) as IReadOnlyDictionary<string, FieldDef>;
            if (headerFields == null || !headerFields.Any())
            {
                Console.WriteLine("  No header fields defined.");
            }
            else
            {
                foreach (var field in headerFields)
                {
                    string tag = field.Key;
                    FieldDef fieldDef = field.Value;
                    if (fieldDef.Type == FieldType.Date)
                    {
                        string dateFormat = dateFormats.TryGetValue($"Header:{tag}", out var df) ? df : "N/A";
                        Console.WriteLine($"  Tag: {tag}, Name: {fieldDef.Name ?? "N/A"}, Type: {fieldDef.Type}, DateFormat: {dateFormat}");
                    }
                    else
                    {
                        Console.WriteLine($"  Tag: {tag}, Name: {fieldDef.Name ?? "N/A"}, Type: {fieldDef.Type}");
                    }
                }
            }

            Console.WriteLine("Message Definitions:");
            if (msgDefs.Count == 0)
            {
                Console.WriteLine("  No message definitions loaded.");
            }
            else
            {
                foreach (var msgDefEntry in msgDefs)
                {
                    string msgType = msgDefEntry.Key;
                    MsgDef msgDef = msgDefEntry.Value;
                    string tableName = msgTableNames.TryGetValue(msgType, out var tn) ? tn : "N/A";
                    Console.WriteLine($"  MsgType: {msgType}, MsgName: {msgDef.Name ?? "N/A"}, Direction: {msgDef.Direction}, TableName: {tableName}");
                    Console.WriteLine("  Fields:");
                    var children = msgDef.GetChildren();
                    if (!children.Any())
                    {
                        Console.WriteLine("    No fields defined.");
                    }
                    else
                    {
                        foreach (var child in children)
                        {
                            string tag = child.Key;
                            if (child.Value is FieldDef fieldDef)
                            {
                                if (fieldDef.Type == FieldType.Date)
                                {
                                    string dateFormat = dateFormats.TryGetValue($"{msgType}:{tag}", out var df) ? df : "yyyyMMdd"; // Default to yyyyMMdd if not specified
                                    Console.WriteLine($"    Tag: {tag}, Name: {fieldDef.Name ?? "N/A"}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField ?? "N/A"}, DateFormat: {dateFormat}");
                                }
                                else
                                {
                                    Console.WriteLine($"    Tag: {tag}, Name: {fieldDef.Name ?? "N/A"}, Type: {fieldDef.Type}, DataField: {fieldDef.DataField ?? "N/A"}");
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

            var filePaths = Directory.GetFiles(folderPath, "*.raw");
            foreach (var filePath in filePaths)
            {
                yield return (Path.GetFileName(filePath), ParseFile(filePath));
            }
        }

        private ParseResult ParseFile(string filePath)
        {
            var errors = new List<string>();
            var allSummaries = new List<MessageSummary>();
            var checksumResults = new List<bool>(); // Track checksum results for each message
            var messageLogs = new List<string>(); // Store detailed logs for all messages

            try
            {
                string rawData = File.ReadAllText(filePath, Encoding.ASCII);
                Console.WriteLine($"Processing raw data of length {rawData.Length} from file: {Path.GetFileName(filePath)}");

                var messages = SplitMessages(rawData);
                if (!messages.Any())
                {
                    errors.Add("No valid FIX messages found in file.");
                    Console.WriteLine($"Warning: No valid messages found in {Path.GetFileName(filePath)}.");
                    return new ParseResult(errors, null);
                }

                Console.WriteLine($"Info: Extracted {messages.Count} messages from raw data of length {rawData.Length}");

                // Process all messages with progress indicator
                for (int i = 0; i < messages.Count; i++)
                {
                    string message = messages[i];
                    try
                    {
                        if (i % 100 == 0 || i == messages.Count - 1) // Log progress every 100 messages or at the end
                        {
                            Console.WriteLine($"Debug: Processing message {i + 1}/{messages.Count} from {Path.GetFileName(filePath)}");
                        }

                        // Parse and log details to memory
                        string logEntry = ParseAndLogMessage(message, i + 1, errors);
                        if (!string.IsNullOrEmpty(logEntry))
                        {
                            messageLogs.Add(logEntry);
                        }

                        var msgSummary = ParseMessage(message);
                        if (msgSummary != null)
                        {
                            allSummaries.Add(msgSummary);
                            if (specs.InMsgChecksum && (i < 5 || i == messages.Count - 1)) // Log checksum for first 5 and last message
                            {
                                bool checksumPassed = VerifyCheckSum(message);
                                checksumResults.Add(checksumPassed);
                                Console.WriteLine($"Debug: CheckSum validation for message {i + 1}: {checksumPassed}");
                            }
                        }
                        else
                        {
                            errors.Add($"Failed to parse message {i + 1} from {Path.GetFileName(filePath)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: Failed to process message {i + 1}/{messages.Count} from {Path.GetFileName(filePath)}: {ex.Message}");
                        errors.Add($"Error processing message {i + 1}: {ex.Message}");
                    }
                }

                // Create parsed_log directory if it doesn't exist
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                // Save detailed logs to file in the parsed_log directory
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");
                File.WriteAllLines(logFilePath, messageLogs);
                Console.WriteLine($"Detailed logs saved to: {logFilePath}");

                // Aggregate summary from all valid results
                if (allSummaries.Any())
                {
                    var representativeSummary = allSummaries.First();
                    representativeSummary.TotalFields = allSummaries.Sum(s => s.TotalFields); // Update total fields across all messages
                    Console.WriteLine($"\nResults for file: {Path.GetFileName(filePath)}");
                    Console.WriteLine($"MsgType: {representativeSummary.MsgType}, MsgName: {representativeSummary.MsgName}, TotalFields: {representativeSummary.TotalFields}");
                }
                else
                {
                    Console.WriteLine($"\nResults for file: {Path.GetFileName(filePath)} - No valid message summary due to errors.");
                }

                if (specs.InMsgChecksum)
                {
                    bool allChecksumsValid = checksumResults.All(result => result);
                    Console.WriteLine($"Debug: CheckSum validation: {allChecksumsValid}");
                }

                // Check for partial message at the end
                int lastBeginIndex = rawData.LastIndexOf($"8={specs.BeginString}");
                if (lastBeginIndex >= 0 && lastBeginIndex + 2 < rawData.Length)
                {
                    Console.WriteLine($"Warning: No BeginString found after index {lastBeginIndex}, possible partial message at end of data (length: {rawData.Length}).");
                }

                return new ParseResult(errors, allSummaries.FirstOrDefault());
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse file {Path.GetFileName(filePath)}: {ex.Message}");
                Console.WriteLine($"Error: {errors.Last()}");
                return new ParseResult(errors, null);
            }
            finally
            {
                // Clear memory for large files
                GC.Collect();
            }
        }

        private string ParseAndLogMessage(string message, int messageNumber, List<string> errors)
        {
            var logLines = new List<string>();

            // Log the raw message
            logLines.Add($"Message {messageNumber}: {message}");

            // Extract and log MsgType
            string? msgType = GetMsgType(message);
            if (string.IsNullOrEmpty(msgType))
            {
                errors.Add($"Failed to extract MsgType from message {messageNumber}");
                return null;
            }
            logLines.Add($"MsgType: {msgType}");

            // Extract and log tags with values
            var tagValuePairs = message.Split(new[] { specs.DelimiterChar }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Split('='))
                .Where(parts => parts.Length == 2)
                .Select(parts => new { Tag = parts[0], Value = parts[1] });
            foreach (var pair in tagValuePairs)
            {
                logLines.Add($"Tags: {pair.Tag} = {pair.Value}");
            }

            return string.Join(Environment.NewLine, logLines);
        }

        private List<string> SplitMessages(string rawData)
        {
            List<string> messages = new List<string>();
            int index = 0;

            while (index < rawData.Length)
            {
                int beginIndex = rawData.IndexOf($"8={specs.BeginString}", index);
                if (beginIndex < 0)
                {
                    if (index < rawData.Length)
                    {
                        Console.WriteLine($"Warning: No BeginString found after index {index}, possible partial message at end of data (length: {rawData.Length}).");
                    }
                    break;
                }

                int bodyLengthStart = rawData.IndexOf("9=", beginIndex) + 2;
                if (bodyLengthStart < 2 || bodyLengthStart >= rawData.Length)
                {
                    Console.WriteLine($"Error: Invalid BodyLength start at index {beginIndex}, discarding data from index {index}.");
                    break;
                }

                int bodyLengthEnd = rawData.IndexOf(specs.DelimiterChar, bodyLengthStart);
                if (bodyLengthEnd < 0)
                {
                    Console.WriteLine($"Error: No delimiter after BodyLength at index {bodyLengthStart}, discarding data from index {index}.");
                    break;
                }

                string bodyLengthStr = rawData.Substring(bodyLengthStart, bodyLengthEnd - bodyLengthStart);
                if (!int.TryParse(bodyLengthStr, out int bodyLength) || bodyLength <= 0)
                {
                    Console.WriteLine($"Error: Invalid BodyLength {bodyLengthStr} at index {beginIndex}, discarding data from index {index}.");
                    break;
                }

                int headerEnd = bodyLengthEnd + 1;
                int bodyEnd = headerEnd + bodyLength;
                int checksumStart = rawData.IndexOf("10=", bodyEnd);
                if (checksumStart < 0 || checksumStart >= rawData.Length - 6) // Ensure room for "10=xxx|"
                {
                    Console.WriteLine($"Error: No valid CheckSum at expected end near {bodyEnd}, discarding data from index {index}.");
                    break;
                }

                int messageEnd = checksumStart + 6; // "10=xxx|" (5 chars + delimiter)
                if (messageEnd > rawData.Length)
                {
                    Console.WriteLine($"Warning: Message end {messageEnd} exceeds data length {rawData.Length}, truncating at {rawData.Length}.");
                    messageEnd = rawData.Length;
                }

                string message = rawData.Substring(beginIndex, messageEnd - beginIndex);
                messages.Add(message);
                index = messageEnd;
            }

            return messages;
        }

        private MessageSummary? ParseMessage(string message)
        {
            string? msgType = GetMsgType(message);
            if (string.IsNullOrEmpty(msgType))
            {
                return null; // Error already logged in ParseFile
            }

            if (!msgDefs.TryGetValue(msgType, out MsgDef msgDef))
            {
                Console.WriteLine($"Error: No MsgDef found for MsgType {msgType}");
                return null;
            }

            Msg msg = new Msg(specs, message);
            var fields = new List<MessageField>();
            var loops = new List<List<MessageField>>();
            int loopStartTag = -1; // Tag to start a loop (e.g., 83)
            List<MessageField> currentLoop = null;

            var fieldPairs = message.Split(new[] { specs.DelimiterChar }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Split('='))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]);

            foreach (var child in msgDef.GetChildren())
            {
                string tag = child.Key;
                if (child.Value is FieldDef fieldDef)
                {
                    string? rawValue = fieldPairs.ContainsKey(tag) ? fieldPairs[tag] : null;
                    string? dateFormat = dateFormats.TryGetValue($"{msgType}:{tag}", out var df) ? df : null;
                    object parsedValue = ParseFieldValue(rawValue, fieldDef.Type, dateFormat);
                    if (tag == "83" && currentLoop != null)
                    {
                        loops.Add(currentLoop);
                        currentLoop = new List<MessageField>();
                    }
                    else if (tag == "83")
                    {
                        loopStartTag = 83;
                        currentLoop = new List<MessageField>();
                    }
                    else if (loopStartTag >= 0 && currentLoop != null)
                    {
                        currentLoop.Add(new MessageField
                        {
                            Tag = tag,
                            Name = fieldDef.Name ?? $"Field_{tag}",
                            Value = parsedValue?.ToString() ?? string.Empty,
                            Type = fieldDef.Type.ToString(),
                            DataField = fieldDef.DataField,
                            ParsedValue = parsedValue
                        });
                    }
                    else
                    {
                        fields.Add(new MessageField
                        {
                            Tag = tag,
                            Name = fieldDef.Name ?? $"Field_{tag}",
                            Value = parsedValue?.ToString() ?? string.Empty,
                            Type = fieldDef.Type.ToString(),
                            DataField = fieldDef.DataField,
                            ParsedValue = parsedValue
                        });
                    }
                }
            }

            if (currentLoop != null && currentLoop.Any())
            {
                loops.Add(currentLoop);
            }

            string tableName = msgTableNames.TryGetValue(msgType, out var tn) ? tn : "";
            string dataField = fields.FirstOrDefault(f => !string.IsNullOrEmpty(f.DataField))?.DataField ?? "";
            string header = (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(dataField)) ? "N/A" : $"{tableName}.{dataField}";

            bool checkSumPassed = !specs.InMsgChecksum || VerifyCheckSum(message);
            Console.WriteLine($"Debug: CheckSum validation: {checkSumPassed}");

            var summary = new MessageSummary
            {
                MsgType = msgType,
                MsgName = msgDef.Name ?? $"MsgType_{msgType}",
                TotalFields = fields.Count + loops.Sum(l => l.Count),
                Header = header
            };
            summary.MessageFields.AddRange(fields); // Add fields to the existing list
            if (loops.Any()) summary.Loops = loops; // Assign loops if any

            return summary;
        }

        private object ParseFieldValue(string? rawValue, FieldType type, string? dateFormat)
        {
            if (string.IsNullOrEmpty(rawValue)) return null;

            return type switch
            {
                FieldType.Int => int.TryParse(rawValue, out int intValue) ? (object)intValue : null,
                FieldType.Double => double.TryParse(rawValue, out double doubleValue) ? (object)doubleValue : null,
                FieldType.Date => ParseDateTime(rawValue, dateFormat),
                _ => rawValue // Default to String
            };
        }

        private DateTime? ParseDateTime(string rawValue, string? dateFormat)
        {
            if (string.IsNullOrEmpty(rawValue)) return null;

            if (!string.IsNullOrEmpty(dateFormat))
            {
                try
                {
                    DateTime parsedDate = DateTime.ParseExact(rawValue, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);
                    int offset = GetTimeZoneOffset(timeZone);
                    return parsedDate.AddHours(offset);
                }
                catch (FormatException)
                {
                    Console.WriteLine($"Warning: Date format {dateFormat} failed for value {rawValue}, falling back to default parsing.");
                }
            }

            if (DateTime.TryParse(rawValue, out DateTime utcDate))
            {
                int offset = GetTimeZoneOffset(timeZone);
                return utcDate.AddHours(offset);
            }
            return null;
        }

        private int GetTimeZoneOffset(string timeZone)
        {
            return timeZone.ToUpper() == "UTC+7" ? 7 : 0; // Vietnam offset
        }

        private string? GetMsgType(string message)
        {
            int msgTypeStart = message.IndexOf("35=") + 3;
            if (msgTypeStart < 3) return null;
            int msgTypeEnd = message.IndexOf(specs.DelimiterChar, msgTypeStart);
            if (msgTypeEnd < 0) return null;
            return message.Substring(msgTypeStart, msgTypeEnd - msgTypeStart);
        }

        private bool VerifyCheckSum(string message)
        {
            int trailerIdx = message.IndexOf("10=");
            if (trailerIdx < 0) return false;

            string dataWithoutChecksum = message.Substring(0, trailerIdx);
            string checksumStr = message.Substring(trailerIdx + 3, message.IndexOf(specs.DelimiterChar, trailerIdx + 3) - (trailerIdx + 3));
            if (!int.TryParse(checksumStr, out int expectedChecksum)) return false;

            int calculatedChecksum = CalculateCheckSum(dataWithoutChecksum);
            bool passed = expectedChecksum == calculatedChecksum;
            Console.WriteLine($"Debug: CheckSum - Expected: {expectedChecksum}, Calculated: {calculatedChecksum}, Passed: {passed}");
            return passed;
        }

        private int CalculateCheckSum(string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            int cks = 0;
            foreach (byte b in buffer)
                cks += b;
            return cks % 256;
        }

        private (Dictionary<string, MsgDef>, Dictionary<string, string>, string, Dictionary<string, string>) LoadMsgDefs(string configPath)
        {
            var defs = new Dictionary<string, MsgDef>();
            var tableNames = new Dictionary<string, string>();
            var dateFormats = new Dictionary<string, string>();
            string loadedTimeZone = "UTC+7"; // Default timezone

            try
            {
                var doc = XDocument.Load(configPath);
                var specsNode = doc.Descendants("Specs").FirstOrDefault();
                if (specsNode != null)
                {
                    loadedTimeZone = specsNode.Attribute("TimeZone")?.Value ?? "UTC+7";

                    var headerFields = specsNode.Descendants("Header").Elements("Field");
                    foreach (var field in headerFields)
                    {
                        string? tag = field.Attribute("Tag")?.Value;
                        string? typeStr = field.Attribute("Type")?.Value;
                        string? dateFormat = field.Attribute("DateFormat")?.Value;
                        if (!string.IsNullOrEmpty(tag) && Enum.TryParse(typeStr, out FieldType type) && type == FieldType.Date)
                        {
                            if (string.IsNullOrEmpty(dateFormat) && tag == "52")
                            {
                                dateFormats[$"Header:{tag}"] = "yyyyMMdd-HH:mm:ss.sss";
                            }
                            else if (!string.IsNullOrEmpty(dateFormat))
                            {
                                dateFormats[$"Header:{tag}"] = dateFormat;
                            }
                        }
                    }
                }

                var messages = doc.Descendants("Msg");
                Console.WriteLine($"Debug: Found {messages.Count()} <Msg> elements.");

                foreach (var msg in messages)
                {
                    string? name = msg.Attribute("Name")?.Value;
                    string? msgType = msg.Attribute("MsgType")?.Value;
                    string? tableName = msg.Attribute("TableName")?.Value;
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(msgType))
                        continue;

                    DataDirection direction = Enum.TryParse(msg.Attribute("Direction")?.Value ?? "In", out DataDirection dir) ? dir : DataDirection.In;

                    var msgDef = new MsgDef(name, msgType, direction);
                    LoadFields(msgDef, msg, msgType, dateFormats);
                    defs[msgType] = msgDef;
                    if (!string.IsNullOrEmpty(tableName))
                        tableNames[msgType] = tableName;
                }

                Console.WriteLine($"Debug: Loaded {defs.Count} message definitions.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to load message definitions: {ex.Message}");
            }

            return (defs, tableNames, loadedTimeZone, dateFormats);
        }

        private void LoadFields(MsgDef msgDef, XElement parentNode, string msgType, Dictionary<string, string> dateFormats)
        {
            var childNodes = parentNode.Elements();
            foreach (var childNode in childNodes)
            {
                if (childNode.Name == "Field")
                {
                    string? tag = childNode.Attribute("Tag")?.Value;
                    string? fieldName = childNode.Attribute("Name")?.Value;
                    string? typeStr = childNode.Attribute("Type")?.Value;
                    string? dataField = childNode.Attribute("DataField")?.Value;
                    string? dateFormat = childNode.Attribute("DateFormat")?.Value;
                    if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(fieldName))
                        continue;

                    FieldType type = Enum.TryParse(typeStr, out FieldType ft) ? ft : FieldType.String;

                    var fieldDef = new FieldDef(tag, fieldName, type, dataField, false, null, false);
                    msgDef.AddChild(tag, fieldDef);

                    if (type == FieldType.Date)
                    {
                        if (!string.IsNullOrEmpty(dateFormat))
                        {
                            dateFormats[$"{msgType}:{tag}"] = dateFormat;
                        }
                        else
                        {
                            dateFormats[$"{msgType}:{tag}"] = "yyyyMMdd";
                        }
                    }
                }
            }
        }
    }

    public class ParseResult
    {
        public List<string> Errors { get; }
        public MessageSummary? MessageSummary { get; }

        public ParseResult(List<string> errors, MessageSummary? messageSummary)
        {
            Errors = errors ?? new List<string>();
            MessageSummary = messageSummary;
        }
    }

    public class MessageSummary
    {
        public string? MsgType { get; set; }
        public string? MsgName { get; set; }
        public int TotalFields { get; set; }
        public string Header { get; set; } = "N/A";
        public List<MessageField> MessageFields { get; } // Read-only, initialized in constructor
        public List<List<MessageField>>? Loops { get; set; }

        public MessageSummary()
        {
            MessageFields = new List<MessageField>();
        }
    }

    public class MessageField
    {
        public string? Tag { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Type { get; set; }
        public string? DataField { get; set; }
        public object? ParsedValue { get; set; }
    }
}