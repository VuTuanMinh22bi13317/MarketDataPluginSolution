using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using MarketDataPlugin.Parsers;

namespace MarketDataPlugin
{
    public class Specs
    {
        public IReadOnlyDictionary<string, FieldDef> HeaderFields { get; private set; }
        public string BeginString { get; private set; } = "FIX.4.4";
        public bool InMsgChecksum { get; private set; } = false;
        public bool OutMsgChecksum { get; private set; } = true;
        public int TimeZone { get; private set; } = 7;
        private readonly Dictionary<string, MsgDef> messages = new Dictionary<string, MsgDef>();

        public Specs(string majorVersion, string minorVersion)
        {
            if (string.IsNullOrEmpty(majorVersion) || string.IsNullOrEmpty(minorVersion))
                throw new ArgumentException("Major and minor versions must not be null or empty.");

            BeginString = "FIX.4.4";
            InMsgChecksum = false;
            OutMsgChecksum = true;
            TimeZone = 7;
            HeaderFields = new Dictionary<string, FieldDef>().AsReadOnly();
        }

        public void Load(string specsFile)
        {
            string xmlContent = ReadXmlContent(specsFile);
            XmlDocument doc = ParseXmlDocument(xmlContent);
            ParseHeader(doc);
            ParseMessages(doc);
        }

        public void DisplayHeader()
        {
            if (HeaderFields == null)
            {
                Console.WriteLine("Header is not loaded.");
                return;
            }

            Console.WriteLine("\nHeader Details:");
            Console.WriteLine("  Header Fields:");
            foreach (var field in HeaderFields)
            {
                var fieldDef = field.Value;
                string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                    ? $", DateFormat={fieldDef.DateFormat}"
                    : "";
                Console.WriteLine($"    FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={(string.IsNullOrEmpty(fieldDef.DataField) ? "<empty>" : fieldDef.DataField)}, IsLog={fieldDef.IsLog}{dateFormatDisplay}");
            }
        }

        public void DisplayMessages()
        {
            if (messages == null || messages.Count == 0)
            {
                Console.WriteLine("Messages are not loaded.");
                return;
            }

            Console.WriteLine("\nMessage Definitions:");
            foreach (var msg in messages)
            {
                var msgDef = msg.Value;
                Console.WriteLine($"  MsgType: {msgDef.MsgType}, Name={msgDef.Name}, Direction={msgDef.Direction}");
                Console.WriteLine("    Fields:");
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                            ? $", DateFormat={fieldDef.DateFormat}"
                            : "";
                        Console.WriteLine($"      FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={(string.IsNullOrEmpty(fieldDef.DataField) ? "<empty>" : fieldDef.DataField)}{dateFormatDisplay}");
                    }
                    else if (child.Value is LoopDef loopDef)
                    {
                        Console.WriteLine($"      LoopDef: Tag={loopDef.Tag}, Name={loopDef.Name}, Counter={loopDef.Counter}");
                        foreach (var loopChild in loopDef.GetChildren())
                        {
                            if (loopChild.Value is FieldDef loopField)
                            {
                                string dateFormatDisplay = loopField.Type == FieldType.Date && !string.IsNullOrEmpty(loopField.DateFormat)
                                    ? $", DateFormat={loopField.DateFormat}"
                                    : "";
                                Console.WriteLine($"        FieldDef: Tag={loopField.Tag}, Name={loopField.Name}, Type={loopField.Type}, DataField={(string.IsNullOrEmpty(loopField.DataField) ? "<empty>" : loopField.DataField)}{dateFormatDisplay}");
                            }
                        }
                    }
                }
            }
        }

        private string ReadXmlContent(string specsFile)
        {
            if (string.IsNullOrEmpty(specsFile) || !File.Exists(specsFile))
                throw new ArgumentException($"Invalid or missing XML file path: {specsFile}", nameof(specsFile));

            string xmlContent = File.ReadAllText(specsFile);
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new InvalidOperationException("XML content is empty.");

            return xmlContent;
        }

        private XmlDocument ParseXmlDocument(string xmlContent)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                var specsNode = doc.SelectSingleNode("/Specs");
                if (specsNode == null)
                    throw new InvalidOperationException("Specs node not found in XML.");

                return doc;
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"Failed to parse XML: {ex.Message} at Line {ex.LineNumber}, Position {ex.LinePosition}", ex);
            }
        }

        private void ParseHeader(XmlDocument doc)
        {
            var headerNode = doc.SelectSingleNode("/Specs/Header");
            if (headerNode == null)
                throw new InvalidOperationException("Header node not found in XML.");

            var headerFields = new Dictionary<string, FieldDef>();

            var childNodes = headerNode.SelectNodes("Field");
            if (childNodes != null)
            {
                foreach (XmlNode childNode in childNodes)
                {
                    var fieldDef = ParseFieldDef(childNode);
                    headerFields[fieldDef.Tag] = fieldDef;
                }
            }

            HeaderFields = headerFields.AsReadOnly();
        }

        private void ParseMessages(XmlDocument doc)
        {
            messages.Clear();

            var msgNodes = doc.SelectNodes("/Specs/Msg");
            if (msgNodes != null)
            {
                foreach (XmlNode msgNode in msgNodes)
                {
                    string name = msgNode.Attributes?["Name"]?.Value ?? "Unknown";
                    string msgType = msgNode.Attributes?["MsgType"]?.Value ?? "Unknown";
                    string directionStr = msgNode.Attributes?["Direction"]?.Value ?? "In";
                    DataDirection direction = GetDirection(directionStr);

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
        }

        private FieldDef ParseFieldDef(XmlNode node)
        {
            string fieldTagValue = node.Attributes?["Tag"]?.Value ?? "";
            string fieldName = node.Attributes?["Name"]?.Value ?? "Unknown";
            string typeStr = node.Attributes?["Type"]?.Value?.Trim() ?? "String";
            FieldType type;
            if (!Enum.TryParse(typeStr, true, out FieldType parsedType))
            {
                type = FieldType.String;
            }
            else
            {
                type = parsedType;
            }

            string? dataField = node.Attributes?["DataField"]?.Value;
            if (dataField == null)
            {
                dataField = node.Attributes?["dataField"]?.Value;
            }
            if (dataField == null)
            {
                dataField = node.Attributes?["DATAFIELD"]?.Value;
            }

            bool isLog = bool.TryParse(node.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
            string? dateFormat = null;

            if (type == FieldType.Date && node.Attributes?["DateFormat"] != null)
            {
                dateFormat = node.Attributes?["DateFormat"]?.Value;
            }

            bool isRequired = bool.TryParse(node.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;

            return new FieldDef(fieldTagValue, fieldName, type, dataField, isLog, dateFormat, isRequired);
        }

        private LoopDef ParseLoopDef(XmlNode node)
        {
            string loopTagValue = node.Attributes?["Tag"]?.Value ?? "";
            string loopName = node.Attributes?["Name"]?.Value ?? "Unknown";
            string counterValue = node.Attributes?["Counter"]?.Value ?? "";

            var loopDef = new LoopDef(loopTagValue, loopName, counterValue);

            var loopChildNodes = node.SelectNodes("Field | Loop");
            if (loopChildNodes != null)
            {
                foreach (XmlNode loopChildNode in loopChildNodes)
                {
                    if (loopChildNode.Name == "Field")
                    {
                        var loopFieldDef = ParseFieldDef(loopChildNode);
                        loopDef.AddChild(loopFieldDef.Tag, loopFieldDef);
                    }
                }
            }

            return loopDef;
        }

        public DataDirection GetDirection(string directionStr)
        {
            if (string.IsNullOrEmpty(directionStr))
            {
                return DataDirection.In;
            }

            var directionMap = new Dictionary<string, DataDirection>(StringComparer.OrdinalIgnoreCase)
            {
                { "in", DataDirection.In },
                { "out", DataDirection.Out },
                { "inbound", DataDirection.Inbound },
                { "outbound", DataDirection.Outbound }
            };

            if (directionMap.TryGetValue(directionStr.Trim(), out DataDirection direction))
            {
                return direction;
            }

            return DataDirection.In;
        }

        public ParseResult? ParseMessage(string fixMessage)
        {
            var result = new ParseResult();
            if (string.IsNullOrEmpty(fixMessage))
            {
                result.AddError("Error: FIX message is empty or null.");
                return result;
            }

            Console.WriteLine($"Debug: Raw fixMessage: {fixMessage.Replace('\u0001', '|')}");

            var fieldsList = new List<KeyValuePair<string, string>>();
            string[] pairs = fixMessage.Split(new[] { '\u0001' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    fieldsList.Add(new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));
                }
                else
                {
                    Console.WriteLine($"Debug: Invalid pair skipped: {pair}");
                }
            }

            Console.WriteLine($"Debug: Fields list count: {fieldsList.Count}");

            // Kiểm tra BeginString (tag 8)
            if (!fieldsList.Any(f => f.Key == "8") || fieldsList.FirstOrDefault(f => f.Key == "8").Value != BeginString)
            {
                result.AddError($"Error: Invalid or missing BeginString. Expected {BeginString}, but got {(fieldsList.Any(f => f.Key == "8") ? fieldsList.FirstOrDefault(f => f.Key == "8").Value ?? "missing" : "missing")}.");
                return result;
            }

            // Kiểm tra MsgType (tag 35)
            var msgTypeEntry = fieldsList.FirstOrDefault(f => f.Key == "35");
            if (msgTypeEntry.Key == null)
            {
                result.AddError("Error: MsgType (Tag 35) not found in FIX message.");
                return result;
            }
            string msgType = msgTypeEntry.Value ?? string.Empty;
            if (string.IsNullOrEmpty(msgType) || !TryGetMessage(msgType, out MsgDef? msgDef))
            {
                result.AddError($"Error: MsgType '{msgType}' not found in Specs.");
                return result;
            }

            // Kiểm tra CheckSum (tag 10)
            if (!fieldsList.Any(f => f.Key == "10"))
            {
                result.AddError("Error: CheckSum (Tag 10) missing.");
                return result;
            }

            // Phân tích message dựa trên cấu hình
            LoopDef? loopDef = null;
            if (msgDef != null)
            {
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is LoopDef loop && loop.Counter == "NO_MD_ENTRIES")
                    {
                        loopDef = loop;
                        break;
                    }
                }
            }

            var messageFields = new List<FieldOutput>();
            var loopFields = new List<List<FieldOutput>>();
            List<FieldOutput> currentLoop = null;
            bool inLoop = false;

            foreach (var field in fieldsList)
            {
                if (field.Key == "10")
                {
                    if (inLoop && currentLoop != null && currentLoop.Count > 0)
                    {
                        foreach (var loopField in currentLoop)
                        {
                            Console.WriteLine($"Debug: Field - Key: {loopField.Tag}, Value: {loopField.Value}");
                        }
                        Console.WriteLine("Debug: Loop end");
                        loopFields.Add(currentLoop);
                        inLoop = false;
                    }
                    Console.WriteLine($"Debug: Field - Key: {field.Key}, Value: {field.Value}");
                    var checkSumField = new FieldOutput
                    {
                        Tag = "10",
                        Value = field.Value,
                        Name = "CheckSum",
                        Type = "String",
                        DataField = "N/A",
                        IsLog = false
                    };
                    messageFields.Add(checkSumField);
                    continue;
                }

                if (field.Key == "83")
                {
                    if (inLoop && currentLoop != null && currentLoop.Count > 0)
                    {
                        foreach (var loopField in currentLoop)
                        {
                            Console.WriteLine($"Debug: Field - Key: {loopField.Tag}, Value: {loopField.Value}");
                        }
                        Console.WriteLine("Debug: Loop end");
                        loopFields.Add(currentLoop);
                    }
                    inLoop = true;
                    currentLoop = new List<FieldOutput>();
                    Console.WriteLine($"Debug: Loop start (NoMDEntries: {field.Value})");
                }

                var fieldOutput = new FieldOutput
                {
                    Tag = field.Key,
                    Value = field.Value
                };

                if (HeaderFields.TryGetValue(field.Key, out FieldDef? headerField))
                {
                    fieldOutput.Name = headerField.Name;
                    fieldOutput.Type = headerField.Type.ToString();
                    fieldOutput.DataField = headerField.DataField ?? "N/A";
                    fieldOutput.IsLog = headerField.IsLog;
                }
                else if (inLoop && loopDef != null)
                {
                    foreach (var child in loopDef.GetChildren())
                    {
                        if (child.Value is FieldDef loopField && loopField.Tag == field.Key)
                        {
                            fieldOutput.Name = loopField.Name;
                            fieldOutput.Type = loopField.Type.ToString();
                            fieldOutput.DataField = loopField.DataField ?? "N/A";
                            fieldOutput.IsLog = null;
                            break;
                        }
                    }
                }
                else if (msgDef != null)
                {
                    foreach (var child in msgDef.GetChildren())
                    {
                        if (child.Value is FieldDef msgField && msgField.Tag == field.Key)
                        {
                            fieldOutput.Name = msgField.Name;
                            fieldOutput.Type = msgField.Type.ToString();
                            fieldOutput.DataField = msgField.DataField ?? "N/A";
                            fieldOutput.IsLog = null;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(fieldOutput.Name))
                {
                    fieldOutput.Name = "Unknown";
                    fieldOutput.Type = "Unknown";
                    fieldOutput.DataField = "N/A";
                    fieldOutput.IsLog = null;
                }

                if (inLoop && currentLoop != null)
                {
                    currentLoop.Add(fieldOutput);
                }
                else
                {
                    Console.WriteLine($"Debug: Field - Key: {field.Key}, Value: {field.Value}");
                    messageFields.Add(fieldOutput);
                }
            }

            if (inLoop && currentLoop != null && currentLoop.Count > 0)
            {
                foreach (var loopField in currentLoop)
                {
                    Console.WriteLine($"Debug: Field - Key: {loopField.Tag}, Value: {loopField.Value}");
                }
                Console.WriteLine("Debug: Loop end");
                loopFields.Add(currentLoop);
            }

            int totalFields = messageFields.Count + loopFields.Sum(l => l.Count);
            result.MessageSummary = new MessageSummary
            {
                MsgType = msgType,
                MsgName = msgDef?.Name ?? "Unknown",
                MessageFields = messageFields,
                Loops = loopFields.Count > 0 ? loopFields : null,
                TotalFields = totalFields
            };

            return result.Success ? result : null;
        }

        public bool TryGetMessage(string msgType, out MsgDef? msgDef)
        {
            return messages.TryGetValue(msgType, out msgDef);
        }

        public IEnumerable<MsgDef> GetAllMessages()
        {
            return messages.Values;
        }
    }
}