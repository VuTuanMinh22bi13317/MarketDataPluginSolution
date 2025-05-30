using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Specs
    {
        public const char DEFAULT_DELIMITER_CHAR = (char)1; // SOH
        public char DelimiterChar { get; }

        public IReadOnlyDictionary<string, FieldDef> HeaderFields { get; }
        public string BeginString { get; }
        public bool InMsgChecksum { get; }
        public bool OutMsgChecksum { get; }
        public string TimeZone { get; } // Changed to string
        public IReadOnlyDictionary<string, MsgDef> Messages { get; }

        public Specs(string majorVersion, string minorVersion, IReadOnlyDictionary<string, FieldDef> headerFields,
                     bool inMsgChecksum, bool outMsgChecksum, string timeZone, IReadOnlyDictionary<string, MsgDef> messages,
                     char? delimiterChar = null)
        {
            if (string.IsNullOrEmpty(majorVersion) || string.IsNullOrEmpty(minorVersion))
                throw new ArgumentException("Major and minor versions must not be null or empty.");

            BeginString = $"FIX.{majorVersion}.{minorVersion}";
            HeaderFields = headerFields ?? new Dictionary<string, FieldDef>().AsReadOnly();
            InMsgChecksum = inMsgChecksum;
            OutMsgChecksum = outMsgChecksum;
            TimeZone = timeZone ?? "UTC+7"; // Default to Vietnam timezone
            Messages = messages ?? new Dictionary<string, MsgDef>().AsReadOnly();
            DelimiterChar = delimiterChar ?? DEFAULT_DELIMITER_CHAR;
        }

        public static Specs Load(string specsFile, string majorVersion, string minorVersion)
        {
            if (string.IsNullOrEmpty(specsFile) || !File.Exists(specsFile))
            {
                Console.WriteLine($"Error: Invalid or missing XML file path: {specsFile}");
                throw new ArgumentException($"Invalid or missing XML file path: {specsFile}", nameof(specsFile));
            }

            string xmlContent = File.ReadAllText(specsFile);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                Console.WriteLine("Error: XML content is empty.");
                throw new InvalidOperationException("XML content is empty.");
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(xmlContent);
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Error: Failed to parse XML: {ex.Message} at Line {ex.LineNumber}, Position {ex.LinePosition}");
                throw;
            }

            var specsNode = doc.SelectSingleNode("/Specs");
            if (specsNode == null)
            {
                Console.WriteLine("Error: Specs node not found in XML.");
                throw new InvalidOperationException("Specs node not found in XML.");
            }

            string beginString = specsNode.Attributes?["BeginString"]?.Value ?? $"FIX.{majorVersion}.{minorVersion}";
            bool inMsgChecksum = bool.TryParse(specsNode.Attributes?["InMsgChecksum"]?.Value, out bool inChecksum) ? inChecksum : false;
            bool outMsgChecksum = bool.TryParse(specsNode.Attributes?["OutMsgChecksum"]?.Value, out bool outChecksum) ? outChecksum : true;
            string timeZone = specsNode.Attributes?["TimeZone"]?.Value ?? "UTC+7";

            var headerFields = new Dictionary<string, FieldDef>();
            var headerNode = doc.SelectSingleNode("/Specs/Header");
            if (headerNode != null)
            {
                var childNodes = headerNode.SelectNodes("Field");
                if (childNodes != null)
                {
                    foreach (XmlNode childNode in childNodes)
                    {
                        string tag = childNode.Attributes?["Tag"]?.Value ?? "";
                        string name = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                        string typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                        FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;

                        string? dataField = childNode.Attributes?["DataField"]?.Value
                                         ?? childNode.Attributes?["dataField"]?.Value
                                         ?? childNode.Attributes?["DATAFIELD"]?.Value
                                         ?? string.Empty;

                        bool isLog = bool.TryParse(childNode.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
                        string? dateFormat = (type == FieldType.Date && childNode.Attributes?["DateFormat"] != null) ? childNode.Attributes?["DateFormat"]?.Value : null;
                        bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;

                        headerFields[tag] = new FieldDef(tag, name, type, dataField, isLog, dateFormat, isRequired);
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
                    var childNodes = msgNode.SelectNodes("Field | Loop");
                    if (childNodes != null)
                    {
                        foreach (XmlNode childNode in childNodes)
                        {
                            if (childNode.Name == "Field")
                            {
                                string tag = childNode.Attributes?["Tag"]?.Value ?? "";
                                string fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                string typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                FieldType type = Enum.TryParse(typeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;

                                string? dataField = childNode.Attributes?["DataField"]?.Value
                                                 ?? childNode.Attributes?["dataField"]?.Value
                                                 ?? childNode.Attributes?["DATAFIELD"]?.Value
                                                 ?? string.Empty;

                                bool isLog = bool.TryParse(childNode.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
                                string? dateFormat = (type == FieldType.Date && childNode.Attributes?["DateFormat"] != null) ? childNode.Attributes?["DateFormat"]?.Value : null;
                                bool isRequired = bool.TryParse(childNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;

                                msgDef.AddChild(tag, new FieldDef(tag, fieldName, type, dataField, isLog, dateFormat, isRequired));
                            }
                            else if (childNode.Name == "Loop")
                            {
                                string tag = childNode.Attributes?["Tag"]?.Value ?? "";
                                string loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                string counter = childNode.Attributes?["Counter"]?.Value ?? "";

                                var loopDef = new LoopDef(tag, loopName, counter);
                                var loopChildNodes = childNode.SelectNodes("Field");
                                if (loopChildNodes != null)
                                {
                                    foreach (XmlNode loopChildNode in loopChildNodes)
                                    {
                                        string loopTag = loopChildNode.Attributes?["Tag"]?.Value ?? "";
                                        string loopFieldName = loopChildNode.Attributes?["Name"]?.Value ?? "Unknown";
                                        string loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String";
                                        FieldType loopType = Enum.TryParse(loopTypeStr, true, out FieldType parsedType) ? parsedType : FieldType.String;

                                        string? loopDataField = loopChildNode.Attributes?["DataField"]?.Value
                                                             ?? loopChildNode.Attributes?["dataField"]?.Value
                                                             ?? loopChildNode.Attributes?["DATAFIELD"]?.Value
                                                             ?? string.Empty;

                                        bool loopIsLog = bool.TryParse(loopChildNode.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
                                        string? loopDateFormat = (loopType == FieldType.Date && loopChildNode.Attributes?["DateFormat"] != null) ? loopChildNode.Attributes?["DateFormat"]?.Value : null;
                                        bool loopIsRequired = bool.TryParse(loopChildNode.Attributes?["IsRequired"]?.Value, out bool required) ? required : false;

                                        loopDef.AddChild(loopTag, new FieldDef(loopTag, loopFieldName, loopType, loopDataField, loopIsLog, loopDateFormat, loopIsRequired));
                                    }
                                }
                                msgDef.AddChild(tag, loopDef);
                            }
                        }
                    }
                    messages[msgType] = msgDef;
                }
            }

            return new Specs(majorVersion, minorVersion, headerFields.AsReadOnly(), inMsgChecksum, outMsgChecksum, timeZone, messages.AsReadOnly());
        }
    }
}