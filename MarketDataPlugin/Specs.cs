using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Generic;

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
            HeaderFields = new Dictionary<string, FieldDef>().AsReadOnly(); // Khởi tạo để tránh cảnh báo
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
                Console.WriteLine($"    FieldDef: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}, IsLog={fieldDef.IsLog}{dateFormatDisplay}");
            }
        }

        private string ReadXmlContent(string specsFile)
        {
            if (string.IsNullOrEmpty(specsFile) || !File.Exists(specsFile))
                throw new ArgumentException("Invalid or missing XML file path.", nameof(specsFile));

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
                Console.WriteLine($"Warning: Unrecognized Type '{typeStr}' for Field Tag={fieldTagValue}, Name={fieldName}. Defaulting to String.");
                type = FieldType.String;
            }
            else
            {
                type = parsedType;
            }
            string? dataField = node.Attributes?["DataField"]?.Value;
            bool isLog = bool.TryParse(node.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
            string? dateFormat = null;

            if (type == FieldType.Date && node.Attributes?["DateFormat"] != null)
            {
                dateFormat = node.Attributes?["DateFormat"]?.Value;
            }

            return new FieldDef(fieldTagValue, fieldName, type, dataField, isLog, dateFormat);
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
                Console.WriteLine("Warning: Direction string is null or empty, defaulting to DataDirection.In");
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

            Console.WriteLine($"Warning: Unrecognized direction '{directionStr}', defaulting to DataDirection.In");
            return DataDirection.In;
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