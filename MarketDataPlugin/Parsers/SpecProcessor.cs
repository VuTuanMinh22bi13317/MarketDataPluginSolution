using MarketDataPlugin;
using MarketDataPlugin.DEF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MarketDataPlugin.Parsers
{
    public class SpecProcessor
    {
        public Specs LoadSpecs(string specsFile, string majorVersion, string minorVersion)
        {
            if (string.IsNullOrEmpty(specsFile) || !File.Exists(specsFile))
                throw new ArgumentException($"Invalid or missing XML file path: {specsFile}", nameof(specsFile));

            string xmlContent = File.ReadAllText(specsFile);
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new InvalidOperationException("XML content is empty.");

            XmlDocument doc = ParseXmlDocument(xmlContent);
            var specsNode = doc.SelectSingleNode("/Specs");
            if (specsNode == null)
                throw new InvalidOperationException("Specs node not found in XML.");

            // Parse attributes
            string beginString = specsNode.Attributes?["BeginString"]?.Value ?? $"FIX.{majorVersion}.{minorVersion}";
            bool inMsgChecksum = bool.TryParse(specsNode.Attributes?["InMsgChecksum"]?.Value, out bool inChecksum) ? inChecksum : false;
            bool outMsgChecksum = bool.TryParse(specsNode.Attributes?["OutMsgChecksum"]?.Value, out bool outChecksum) ? outChecksum : true;
            int timeZoneInt = int.TryParse(specsNode.Attributes?["TimeZone"]?.Value, out int tz) ? tz : 7;
            string timeZone = $"UTC+{timeZoneInt}"; // Convert int to string (e.g., 7 -> "UTC+7")

            // Parse header
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

            // Parse messages
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

            return new Specs(majorVersion, minorVersion, headerFields.AsReadOnly(), inMsgChecksum, outMsgChecksum, timeZone, messages.AsReadOnly())
                ?? throw new ArgumentException("Failed to initialize specs.", nameof(specsFile));
        }

        private XmlDocument ParseXmlDocument(string xmlContent)
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
            string? dateFormat = null;
            if (type == FieldType.Date && node.Attributes?["DateFormat"] != null)
                dateFormat = node.Attributes?["DateFormat"]?.Value;

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
    }
}