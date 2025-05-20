using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Generic;
using MarketDataPlugin.Interfaces;

namespace MarketDataPlugin
{
    public class Specs
    {
        public void Load(string specsFile)
        {
            // Bước 1: Đọc nội dung file XML
            string xmlContent = ReadXmlContent(specsFile);

            // Bước 2: Parse XML thành XmlDocument
            XmlDocument doc = ParseXmlDocument(xmlContent);

            // Bước 3: Cập nhật các thuộc tính từ XML
            UpdateAttributes(doc);

            // Bước 4: Parse các message và loop
            ParseMessages(doc);
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

        private void UpdateAttributes(XmlDocument doc)
        {
            var specsNode = doc.SelectSingleNode("/Specs");
            BeginString = specsNode.Attributes?["BeginString"]?.Value ?? BeginString;
            InMsgChecksum = bool.TryParse(specsNode.Attributes?["InMsgChecksum"]?.Value, out bool inChecksum) ? inChecksum : InMsgChecksum;
            OutMsgChecksum = bool.TryParse(specsNode.Attributes?["OutMsgChecksum"]?.Value, out bool outChecksum) ? outChecksum : OutMsgChecksum;
            TimeZone = int.TryParse(specsNode.Attributes?["TimeZone"]?.Value, out int timeZone) ? timeZone : TimeZone;
        }

        private void ParseMessages(XmlDocument doc)
        {
            messages.Clear();
            loops.Clear();

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
                    messages.Add(msgDef);

                    var childNodes = msgNode.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            bool required = ParseRequiredFromComment(childNode);

                            if (childNode.Name == "Field")
                            {
                                var fieldDef = ParseFieldDef(childNode);
                                msgDef.AddChild(fieldDef);
                            }
                            else if (childNode.Name == "Loop")
                            {
                                var loopDef = ParseLoopDef(childNode);
                                msgDef.AddChild(loopDef);
                                loops.Add(loopDef);
                            }
                        }
                    }
                }
            }
        }

        private bool ParseRequiredFromComment(XmlNode node)
        {
            string commentText = "";
            var previousNode = node.PreviousSibling;
            while (previousNode != null && previousNode.NodeType == XmlNodeType.Comment)
            {
                commentText = previousNode.InnerText + "\n" + commentText;
                previousNode = previousNode.PreviousSibling;
            }

            if (!string.IsNullOrEmpty(commentText))
            {
                var match = Regex.Match(commentText, @"Required:\s*(Y|N)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.ToUpper() == "Y";
                }
            }
            return false;
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

            bool required = ParseRequiredFromComment(node);
            return new FieldDef(fieldTagValue, fieldName, type, required, dataField, isLog, dateFormat);
        }

        private LoopDef ParseLoopDef(XmlNode node)
        {
            string loopTagValue = node.Attributes?["Tag"]?.Value ?? "";
            string loopName = node.Attributes?["Name"]?.Value ?? "Unknown";
            string counterValue = node.Attributes?["Counter"]?.Value ?? "";
            bool required = ParseRequiredFromComment(node);

            var loopDef = new LoopDef(loopTagValue, loopName, counterValue, required);

            var loopChildNodes = node.SelectNodes("Field | Loop");
            if (loopChildNodes != null)
            {
                foreach (XmlNode loopChildNode in loopChildNodes)
                {
                    if (loopChildNode.Name == "Field")
                    {
                        var loopFieldDef = ParseFieldDef(loopChildNode);
                        loopDef.AddChild(loopFieldDef);
                    }
                }
            }

            return loopDef;
        }

        public string BeginString { get; private set; }
        public bool InMsgChecksum { get; private set; }
        public bool OutMsgChecksum { get; private set; }
        public int TimeZone { get; private set; }
        private readonly List<IMessageDef> messages = new List<IMessageDef>();
        private readonly List<ILoopDef> loops = new List<ILoopDef>();
        public IReadOnlyList<IMessageDef> Messages => messages.AsReadOnly();
        public IReadOnlyList<ILoopDef> Loops => loops.AsReadOnly();

        public Specs(string majorVersion, string minorVersion)
        {
            if (string.IsNullOrEmpty(majorVersion) || string.IsNullOrEmpty(minorVersion))
                throw new ArgumentException("Major and minor versions must not be null or empty.");

            BeginString = $"FIX.{majorVersion}.{minorVersion}";
            InMsgChecksum = false; // Giá trị mặc định
            OutMsgChecksum = true; // Giá trị mặc định
            TimeZone = 7; // Giá trị mặc định
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
    }
}