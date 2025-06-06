using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using MarketDataPlugin;
using MarketDataPlugin.DEF;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace MarketDataPlugin
{
    public class FixMessageParser
    {
        // Configuration fields
        private readonly string beginString = "FIX.4.4";
        private readonly char delimiterChar = '\x01';
        private readonly bool inMsgChecksum = true; // Set to false to skip checksum validation
        private readonly Dictionary<string, FieldDef> headerFields = new Dictionary<string, FieldDef>();
        private readonly Dictionary<string, MsgDef> msgDefs = new Dictionary<string, MsgDef>();
        private readonly Dictionary<string, string> dateFormats = new Dictionary<string, string>();
        private readonly Dictionary<string, string> msgTableNames = new Dictionary<string, string>();
        private readonly string timeZone = "7";
        private readonly Dictionary<string, string> fieldNameToTag = new Dictionary<string, string>();

        private readonly Msg msgParser;

        // Nested Msg class
        private class Msg
        {
            private readonly FixMessageParser parser;

            public Msg(FixMessageParser parser)
            {
                this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
            }

            public ParseResult Parse(string? rawMessage)
            {
                var errors = new List<string>();
                var fields = new List<MessageField>();
                var loops = new List<Loop>();
                var unparsedFields = new List<string>();
                int messageLength = rawMessage?.Length ?? 0;
                bool checkSumPassed = true; // Track checksum validation for this message

                if (string.IsNullOrEmpty(rawMessage))
                {
                    errors.Add("Empty message");
                    return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: false);
                }

                // Verify checksum on raw message first
                if (parser.inMsgChecksum)
                {
                    int trailerIdx = rawMessage!.LastIndexOf($"{parser.delimiterChar}10=");
                    if (trailerIdx < 0)
                    {
                        errors.Add("Missing checksum (Tag 10)");
                        return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: false);
                    }
                    string body = rawMessage.Substring(0, trailerIdx);
                    int calculated = CalculateCheckSum(body);
                    string checkSumValue = rawMessage.Substring(trailerIdx + 4, 3);
                    if (!int.TryParse(checkSumValue, out int checkSum) || calculated != checkSum)
                    {
                        errors.Add($"Checksum failed: Calculated {calculated}, Expected {checkSumValue}");
                        checkSumPassed = false; // Mark checksum as failed but continue parsing
                    }
                }

                if (!rawMessage!.StartsWith($"8={parser.beginString}{parser.delimiterChar}"))
                {
                    errors.Add($"Invalid BeginString: Expected '8={parser.beginString}', got '{rawMessage.Substring(0, Math.Min(10, rawMessage.Length))}'");
                    return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: checkSumPassed);
                }

                string msgType = GetMsgType(rawMessage);
                if (string.IsNullOrEmpty(msgType))
                {
                    errors.Add("Invalid MsgType: Could not extract MsgType (tag 35)");
                    return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: checkSumPassed);
                }

                if (!parser.msgDefs.TryGetValue(msgType, out MsgDef msgDef))
                {
                    errors.Add($"Invalid MsgType: '{msgType}' not found in message definitions");
                    return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: checkSumPassed);
                }

                string[] fieldPairs = rawMessage.Split(parser.delimiterChar);
                int currentIndex = 0;

                // Parse header fields
                while (currentIndex < fieldPairs.Length)
                {
                    string pair = fieldPairs[currentIndex];
                    int equalsIndex = pair.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string tag = pair.Substring(0, equalsIndex);
                        string value = pair.Substring(equalsIndex + 1);
                        if (parser.headerFields.TryGetValue(tag, out FieldDef headerField))
                        {
                            var field = ParseField(tag, value, headerField, msgType);
                            fields.Add(field);
                            currentIndex++;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    currentIndex++;
                }

                // Parse body fields and loops
                while (currentIndex < fieldPairs.Length)
                {
                    string pair = fieldPairs[currentIndex];
                    int equalsIndex = pair.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string tag = pair.Substring(0, equalsIndex);
                        string value = pair.Substring(equalsIndex + 1);

                        if (tag == "10")
                        {
                            var fieldDef = new FieldDef(tag, "CheckSum") { Type = FieldType.String };
                            var field = ParseField(tag, value, fieldDef, msgType);
                            fields.Add(field);
                            currentIndex++;
                            break;
                        }

                        if (msgDef.GetChildren().TryGetValue(tag, out object childObj))
                        {
                            if (childObj is FieldDef fieldDef)
                            {
                                var field = ParseField(tag, value, fieldDef, msgType);
                                fields.Add(field);
                                currentIndex++;
                                continue;
                            }
                            else if (childObj is LoopDef loopDef)
                            {
                                string counterTag = loopDef.Counter ?? string.Empty;
                                if (pair.StartsWith($"{counterTag}="))
                                {
                                    if (int.TryParse(value, out int loopCount) && loopCount > 0)
                                    {
                                        currentIndex++;
                                        int expectedLoopCount = loopCount;
                                        string loopStartTag = "83";

                                        for (int loopEntry = 0; loopEntry < expectedLoopCount && currentIndex < fieldPairs.Length; loopEntry++)
                                        {
                                            var currentLoop = new Loop(counterTag, loopDef.Name ?? string.Empty, counterTag);
                                            foreach (var child in loopDef.GetChildren())
                                            {
                                                currentLoop.AddChild(child.Key, child.Value);
                                            }

                                            bool loopStarted = false;
                                            while (currentIndex < fieldPairs.Length && !loopStarted)
                                            {
                                                if (fieldPairs[currentIndex].StartsWith($"{loopStartTag}="))
                                                {
                                                    loopStarted = true;
                                                    break;
                                                }
                                                currentIndex++;
                                            }

                                            if (!loopStarted)
                                            {
                                                break;
                                            }

                                            while (currentIndex < fieldPairs.Length)
                                            {
                                                string loopPair = fieldPairs[currentIndex];
                                                int loopEqualsIndex = loopPair.IndexOf('=');
                                                if (loopEqualsIndex > 0)
                                                {
                                                    string loopTag = loopPair.Substring(0, loopEqualsIndex);
                                                    string loopValue = loopPair.Substring(loopEqualsIndex + 1);

                                                    if ((loopTag == loopStartTag && currentLoop.ParsedFields.Count > 0) || loopTag == "10")
                                                    {
                                                        if (loopTag == loopStartTag) currentIndex--;
                                                        break;
                                                    }

                                                    if (loopDef.GetChildren().TryGetValue(loopTag, out FieldDef loopFieldDef))
                                                    {
                                                        var field = ParseField(loopTag, loopValue, loopFieldDef, msgType);
                                                        currentLoop.AddParsedField(field);
                                                        if (field.Name == "MarketId" || field.Name == "Symbol" || field.Name == "TransactTime")
                                                        {
                                                            loopFieldsDict[field.Name] = field.ParsedValue ?? string.Empty;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        unparsedFields.Add(loopPair);
                                                    }
                                                }
                                                currentIndex++;
                                            }

                                            if (currentLoop.ParsedFields.Count > 0)
                                            {
                                                loops.Add(currentLoop);
                                            }
                                        }
                                        continue;
                                    }
                                }
                            }
                        }

                        unparsedFields.Add(pair);
                        currentIndex++;
                    }
                    else
                    {
                        currentIndex++;
                    }
                }

                // Check for required fields
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                    {
                        if (!fields.Exists(f => f.Tag == fieldDef.Tag.ToString()))
                        {
                            errors.Add($"Missing required field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                            return new ParseResult(errors, fields, loops, string.Empty, checkSumPassed: checkSumPassed);
                        }
                    }
                }

                string unparsedText = unparsedFields.Count > 0 ? string.Join(parser.delimiterChar.ToString(), unparsedFields) : string.Empty;
                return new ParseResult(errors, fields, loops, unparsedText, checkSumPassed: checkSumPassed);
            }

            private int CalculateCheckSum(string str)
            {
                byte[] buffer = Encoding.ASCII.GetBytes(str ?? string.Empty);
                int cks = 0;
                for (int i = 0; i < buffer.Length; i++) cks += buffer[i];
                return cks % 256;
            }

            private MessageField ParseField(string tag, string value, FieldDef fieldDef, string msgType)
            {
                string dateFormat = fieldDef.DateFormat ?? string.Empty;
                if (fieldDef.Type == FieldType.Date && parser.dateFormats.TryGetValue($"{msgType}:{tag}", out string format))
                {
                    dateFormat = format ?? string.Empty;
                }
                object parsedValue = ParseFieldValue(value, fieldDef.Type, dateFormat, fieldDef.TimeZoneEffected);
                return new MessageField
                {
                    Tag = tag,
                    Name = fieldDef.Name,
                    Value = value,
                    Type = fieldDef.Type.ToString(),
                    DataField = fieldDef.DataField,
                    ParsedValue = parsedValue
                };
            }

            private object ParseFieldValue(string value, FieldType type, string dateFormat, bool timeZoneEffected)
            {
                if (string.IsNullOrEmpty(value)) return string.Empty;
                switch (type)
                {
                    case FieldType.Int: return int.TryParse(value, out int i) ? (object)i : string.Empty;
                    case FieldType.Double: return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (object)d : string.Empty;
                    case FieldType.Date:
                        try
                        {
                            if (!string.IsNullOrEmpty(dateFormat))
                            {
                                if (dateFormat == "HHmmssfff" || dateFormat == "HHmmss")
                                {
                                    DateTime baseDate = DateTime.UtcNow.Date;
                                    DateTime time = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                                    DateTime combined = baseDate.Add(time.TimeOfDay);
                                    if (timeZoneEffected)
                                        combined = combined.AddHours(7);
                                    return combined;
                                }
                                else
                                {
                                    DateTime dt = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                                    if (timeZoneEffected)
                                        dt = dt.AddHours(7);
                                    return dt;
                                }
                            }
                            else if (DateTime.TryParse(value, out DateTime dtDefault))
                            {
                                if (timeZoneEffected)
                                    dtDefault = dtDefault.AddHours(7);
                                return dtDefault;
                            }
                        }
                        catch (FormatException)
                        {
                            return string.Empty;
                        }
                        return string.Empty;
                    default: return value;
                }
            }

            public string GetMsgType(string message)
            {
                string[] parts = message.Split(parser.delimiterChar);
                foreach (var part in parts)
                {
                    if (part.StartsWith("35=")) return part.Substring(3);
                }
                return string.Empty;
            }

            private readonly Dictionary<string, object> loopFieldsDict = new Dictionary<string, object>();
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
                string tagStr = node.Attributes?["Tag"]?.Value ?? throw new ArgumentException($"Missing Tag attribute in Header Field node");
                if (!int.TryParse(tagStr, out int tag))
                    throw new ArgumentException($"Invalid Tag value '{tagStr}' in Header Field node");
                string name = node.Attributes?["Name"]?.Value ?? throw new ArgumentException($"Missing Name attribute in Header Field node");
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException($"Name attribute cannot be empty in Header Field node with Tag '{tagStr}'");
                string typeStr = node.Attributes?["Type"]?.Value?.Trim() ?? "String";
                FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                string dataField = node.Attributes?["DataField"]?.Value ?? string.Empty;
                bool timeZoneEffected = bool.TryParse(node.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                string dateFormat = type == FieldType.Date && node.Attributes?["DateFormat"] != null ? node.Attributes["DateFormat"].Value : string.Empty;

                var fieldDef = new FieldDef(tagStr, name)
                {
                    Type = type,
                    DataField = dataField,
                    TimeZoneEffected = timeZoneEffected,
                    DateFormat = dateFormat,
                    Tag = tag
                };
                headerFields[tagStr] = fieldDef;
            }

            // Load message definitions
            XmlNodeList? msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes == null)
            {
                throw new ArgumentException($"No <Msg> nodes found in config file {configPath}");
            }
            foreach (XmlNode node in msgNodes)
            {
                string msgType = node.Attributes?["MsgType"]?.Value ?? throw new ArgumentException($"Missing MsgType attribute in Msg node");
                string name = node.Attributes?["Name"]?.Value ?? throw new ArgumentException($"Missing Name attribute in Msg node");
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException($"Name attribute cannot be empty in Msg node with MsgType '{msgType}'");
                string tableName = node.Attributes?["TableName"]?.Value ?? string.Empty;
                msgTableNames[msgType] = tableName;
                if (Enum.TryParse<DataDirection>(node.Attributes?["Direction"]?.Value ?? "InOut", out var direction))
                {
                    var msgDef = new MsgDef(name, msgType, direction);
                    msgDefs[msgType] = msgDef;

                    var childNodes = node.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        fieldNameToTag.Clear();
                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string tag = childNode.Attributes?["Tag"]?.Value ?? string.Empty;
                                string fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                fieldNameToTag[fieldName] = tag;
                            }
                        }

                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string tagStr = childNode.Attributes?["Tag"]?.Value ?? throw new ArgumentException($"Missing Tag attribute in Field node");
                                if (!int.TryParse(tagStr, out int tag))
                                    throw new ArgumentException($"Invalid Tag value '{tagStr}' in Field node");
                                string fieldName = childNode.Attributes?["Name"]?.Value ?? throw new ArgumentException($"Missing Name attribute in Field node");
                                if (string.IsNullOrWhiteSpace(fieldName))
                                    throw new ArgumentException($"Name attribute cannot be empty in Field node with tag '{tagStr}' in MsgType '{msgType}'");
                                string typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                string dataField = childNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                bool timeZoneEffected = bool.TryParse(childNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                string dateFormat = type == FieldType.Date && childNode.Attributes?["DateFormat"] != null ? childNode.Attributes["DateFormat"].Value : string.Empty;

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
                                    dateFormats[$"{msgType}:{tagStr}"] = dateFormat;
                            }
                            else if (childNode.Name == "Loop")
                            {
                                string counterName = childNode.Attributes?["Counter"]?.Value ?? string.Empty;
                                string counterTag = fieldNameToTag.TryGetValue(counterName, out var tag) ? tag : counterName;
                                string loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                var children = new Dictionary<string, FieldDef>();
                                var loopDef = new LoopDef(counterTag, loopName, children);
                                var loopChildNodes = childNode.SelectNodes("Field");
                                if (loopChildNodes != null)
                                {
                                    foreach (XmlNode loopChildNode in loopChildNodes)
                                    {
                                        string loopTagStr = loopChildNode.Attributes?["Tag"]?.Value ?? string.Empty;
                                        if (!int.TryParse(loopTagStr, out int loopTag))
                                            throw new ArgumentException($"Invalid Tag value '{loopTagStr}' in Loop Field node");
                                        string loopFieldName = loopChildNode.Attributes?["Name"]?.Value ?? "Unknown";
                                        if (string.IsNullOrWhiteSpace(loopFieldName))
                                            throw new ArgumentException($"Name attribute cannot be empty in Loop Field node with tag '{loopTagStr}' in MsgType '{msgType}'");
                                        string loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                        FieldType loopType = Enum.TryParse(loopTypeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;
                                        string loopDataField = loopChildNode.Attributes?["DataField"]?.Value ?? string.Empty;
                                        bool loopIsRequired = bool.TryParse(loopChildNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;
                                        bool loopTimeZoneEffected = bool.TryParse(loopChildNode.Attributes?["TimeZoneEffected"]?.Value, out bool tze) && tze;
                                        string loopDateFormat = loopType == FieldType.Date && loopChildNode.Attributes?["DateFormat"] != null ? loopChildNode.Attributes["DateFormat"].Value : string.Empty;

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
                                            dateFormats[$"{msgType}:{loopTagStr}"] = loopDateFormat;
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
            Console.WriteLine($"InMsgChecksum: {inMsgChecksum}");
            Console.WriteLine($"TimeZone: {timeZone} (Offset: +7 hours - Vietnam)");
            Console.WriteLine($"DelimiterChar: {(int)delimiterChar}");
            Console.WriteLine($"BeginString: {beginString}");

            Console.WriteLine("Header Fields:");
            foreach (var field in headerFields)
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
                yield return ("", new ParseResult(new List<string> { $"Error: Directory not found - {folderPath}" }, new List<MessageField>(), new List<Loop>(), string.Empty, checkSumPassed: true));
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
            const int batchSize = 1000;
            bool allCheckSumsPassed = true; // Track if all messages pass checksum validation

            try
            {
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");

                var rawData = File.ReadAllText(filePath);
                Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");

                // Inline message splitting logic
                var messages = new List<string>();
                Console.WriteLine($"Debug: Expected BeginString: 8={beginString}");

                int index = 0;
                while (index < rawData.Length)
                {
                    int beginStringIndex = rawData.IndexOf($"8={beginString}", index);
                    if (beginStringIndex == -1)
                    {
                        Console.WriteLine("Debug: No more BeginString found, ending message splitting.");
                        break;
                    }

                    Console.WriteLine($"Debug: Checking segment at index {beginStringIndex}, starts with: {rawData.Substring(beginStringIndex, Math.Min(12, rawData.Length - beginStringIndex))}...");

                    index = beginStringIndex;

                    int bodyLengthStart = index + $"8={beginString}".Length;
                    if (bodyLengthStart >= rawData.Length || rawData[bodyLengthStart] != delimiterChar)
                    {
                        Console.WriteLine($"Debug: Invalid format after BeginString at index {index}, skipping.");
                        index++;
                        continue;
                    }

                    // Ensure the next tag is 9=
                    int bodyLengthTagStart = bodyLengthStart + 1; // Position after the delimiter
                    if (bodyLengthTagStart + 2 >= rawData.Length || rawData.Substring(bodyLengthTagStart, 2) != "9=")
                    {
                        Console.WriteLine($"Debug: Expected '9=' at index {bodyLengthTagStart}, but found '{rawData.Substring(bodyLengthTagStart, Math.Min(2, rawData.Length - bodyLengthTagStart))}', skipping.");
                        index++;
                        continue;
                    }

                    // Move past the "9=" to get the value
                    int bodyLengthValueStart = bodyLengthTagStart + 2; // Position after "9="
                    int bodyLengthTagEnd = rawData.IndexOf(delimiterChar, bodyLengthValueStart);
                    if (bodyLengthTagEnd == -1)
                    {
                        Console.WriteLine($"Debug: Could not find end of BodyLength tag at index {bodyLengthValueStart}, ending.");
                        break;
                    }

                    string bodyLengthStr = rawData.Substring(bodyLengthValueStart, bodyLengthTagEnd - bodyLengthValueStart);
                    if (!int.TryParse(bodyLengthStr, out int bodyLength))
                    {
                        Console.WriteLine($"Debug: Invalid BodyLength value '{bodyLengthStr}' at index {bodyLengthValueStart}, skipping.");
                        index++;
                        continue;
                    }

                    Console.WriteLine($"Debug: Found BodyLength at index {bodyLengthValueStart}: {bodyLength}");

                    // Calculate the start of the body (after 9=<length><delimiter>)
                    int bodyStart = bodyLengthTagEnd + 1;

                    // The BodyLength is the number of bytes from bodyStart to the start of 10=
                    int bodyEnd = bodyStart + bodyLength;
                    if (bodyEnd + 3 >= rawData.Length)
                    {
                        Console.WriteLine($"Debug: Body end {bodyEnd} exceeds raw data length {rawData.Length}, skipping.");
                        index++;
                        continue;
                    }

                    // Search for the 10= tag after the body
                    int checksumStart = rawData.IndexOf($"10=", bodyEnd - 5);
                    if (checksumStart == -1)
                    {
                        Console.WriteLine($"Debug: No '10=' tag found after body end {bodyEnd}, skipping.");
                        index++;
                        continue;
                    }

                    Console.WriteLine($"Debug: Found '10=' tag at index {checksumStart}, expected around {bodyEnd}");

                    // Find the end of the checksum field
                    int checksumEnd = rawData.IndexOf(delimiterChar, checksumStart + 3);
                    if (checksumEnd == -1)
                    {
                        Console.WriteLine($"Debug: No valid checksum delimiter after '10=' at {checksumStart}, skipping.");
                        index++;
                        continue;
                    }

                    // Extract the message
                    string message = rawData.Substring(index, checksumEnd + 1 - index);
                    messages.Add(message);
                    Console.WriteLine($"Debug: Successfully extracted message of length {message.Length} from index {index} to {checksumEnd + 1}");
                    index = checksumEnd + 1;
                }

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

                var allFields = new List<MessageField>();
                var allLoops = new List<Loop>();
                foreach (var result in parseResults)
                {
                    if (!string.IsNullOrEmpty(result.logEntry))
                    {
                        logBatch.Add((result.index, result.logEntry));
                    }
                    if (result.result.Errors.Count > 0)
                    {
                        errors.AddRange(result.result.Errors);
                    }
                    allFields.AddRange(result.result.Fields ?? new List<MessageField>());
                    allLoops.AddRange(result.result.Loops ?? new List<Loop>());
                    allCheckSumsPassed &= result.result.CheckSumPassed; // Update overall checksum status

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
                // If not checking checksum, set CheckSumPassed to true as per the rule
                bool finalCheckSumPassed = inMsgChecksum ? allCheckSumsPassed : true;
                return new ParseResult(errors, allFields, allLoops, string.Empty, finalCheckSumPassed);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse file: {Path.GetFileName(filePath)}: {ex.Message}");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, checkSumPassed: true); // Per rule: default to true if error occurs
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

        private string ParseAndLogMessage(string? message, int messageNumber, List<string> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Message {messageNumber} (Length: {message?.Length ?? 0}):");

            var parseResult = msgParser.Parse(message);
            if (parseResult.Errors.Count > 0)
            {
                errors.AddRange(parseResult.Errors);
                sb.AppendLine("Failed to parse message:");
                foreach (var error in parseResult.Errors)
                {
                    sb.AppendLine($"  Error: {error}");
                }
                return sb.ToString();
            }

            foreach (var field in parseResult.Fields ?? new List<MessageField>())
            {
                if (headerFields.ContainsKey(field.Tag))
                {
                    sb.AppendLine($"  Header - Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                }
            }

            foreach (var field in parseResult.Fields ?? new List<MessageField>())
            {
                if (!headerFields.ContainsKey(field.Tag) && field.Tag != "10")
                {
                    sb.AppendLine($"  Body - Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                }
            }

            for (int i = 0; i < (parseResult.Loops?.Count ?? 0); i++)
            {
                var loop = parseResult.Loops?[i];
                sb.AppendLine($"  Loop Entry {i + 1}:");
                foreach (var field in loop?.ParsedFields ?? new List<MessageField>())
                {
                    sb.AppendLine($"    Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                }
            }

            foreach (var field in parseResult.Fields ?? new List<MessageField>())
            {
                if (field.Tag == "10")
                {
                    sb.AppendLine($"  Trailer - Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, ParsedValue: {field.ParsedValue ?? "N/A"}");
                }
            }

            if (!string.IsNullOrEmpty(parseResult.UnparsedText))
            {
                sb.AppendLine($"  Unparsed Fields: {parseResult.UnparsedText}");
            }

            string[] allFieldPairs = (message?.Split(delimiterChar)) ?? Array.Empty<string>();
            sb.AppendLine("  All Detected Tags:");
            foreach (var pair in allFieldPairs)
            {
                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);
                    string fieldName = (parseResult.Fields?.FirstOrDefault(f => f.Tag == tag)?.Name) ?? "Unknown";
                    sb.AppendLine($"    Tag: {tag}, Name: {fieldName}, Value: {value}");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}