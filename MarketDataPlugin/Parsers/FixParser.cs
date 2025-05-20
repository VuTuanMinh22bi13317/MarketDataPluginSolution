using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace MarketDataPlugin
{
    public static class FixParser
    {
        public static void ParseSpecsFromXml(string xmlFilePath, Specs specs)
        {
            if (string.IsNullOrEmpty(xmlFilePath) || !File.Exists(xmlFilePath))
                throw new ArgumentException("Invalid or missing XML file path.", nameof(xmlFilePath));

            string xmlContent = File.ReadAllText(xmlFilePath);
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new InvalidOperationException("XML content is empty.");

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                var specsNode = doc.SelectSingleNode("/Specs");
                if (specsNode != null)
                {
                    specs.BeginString = specsNode.Attributes?["BeginString"]?.Value ?? "FIX.4.4";
                    specs.InMsgChecksum = bool.TryParse(specsNode.Attributes?["InMsgChecksum"]?.Value, out bool inChecksum) ? inChecksum : false;
                    specs.OutMsgChecksum = bool.TryParse(specsNode.Attributes?["OutMsgChecksum"]?.Value, out bool outChecksum) ? outChecksum : true;
                    specs.TimeZone = int.TryParse(specsNode.Attributes?["TimeZone"]?.Value, out int timeZone) ? timeZone : 7;

                    specs.Messages.Clear();
                    specs.Loops.Clear();

                    var msgNodes = specsNode.SelectNodes("Msg");
                    if (msgNodes != null)
                    {
                        foreach (XmlNode msgNode in msgNodes)
                        {
                            string name = msgNode.Attributes?["Name"]?.Value ?? "Unknown";
                            string msgType = msgNode.Attributes?["MsgType"]?.Value ?? "Unknown";
                            string directionStr = msgNode.Attributes?["Direction"]?.Value ?? "In";
                            DataDirection direction = specs.GetDirection(directionStr);

                            var msgDef = new MsgDef(name, msgType, direction);
                            specs.Messages.Add(msgDef);

                            var childNodes = msgNode.SelectNodes("Field | Loop");
                            if (childNodes != null)
                            {
                                foreach (XmlNode childNode in childNodes)
                                {
                                    string commentText = "";
                                    var previousNode = childNode.PreviousSibling;
                                    while (previousNode != null && previousNode.NodeType == XmlNodeType.Comment)
                                    {
                                        commentText = previousNode.InnerText + "\n" + commentText;
                                        previousNode = previousNode.PreviousSibling;
                                    }

                                    bool required = false;
                                    if (!string.IsNullOrEmpty(commentText))
                                    {
                                        var match = Regex.Match(commentText, @"Required:\s*(Y|N)", RegexOptions.IgnoreCase);
                                        if (match.Success)
                                        {
                                            required = match.Groups[1].Value.ToUpper() == "Y";
                                        }
                                    }

                                    if (childNode.Name == "Field")
                                    {
                                        string fieldTagValue = childNode.Attributes?["Tag"]?.Value ?? "";
                                        string fieldName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                        string typeStr = childNode.Attributes?["Type"]?.Value?.Trim() ?? "String"; // Thêm Trim() để loại bỏ khoảng trắng
                                        FieldType type;
                                        if (!Enum.TryParse(typeStr, true, out FieldType parsedType)) // ignoreCase: true
                                        {
                                            Console.WriteLine($"Warning: Unrecognized Type '{typeStr}' for Field Tag={fieldTagValue}, Name={fieldName}. Defaulting to String.");
                                            type = FieldType.String;
                                        }
                                        else
                                        {
                                            type = parsedType;
                                        }
                                        string? dataField = childNode.Attributes?["DataField"]?.Value;
                                        bool isLog = bool.TryParse(childNode.Attributes?["IsLog"]?.Value, out bool log) ? log : false;
                                        string? dateFormat = null;

                                        if (type == FieldType.Date && childNode.Attributes?["DateFormat"] != null)
                                        {
                                            dateFormat = childNode.Attributes?["DateFormat"]?.Value;
                                        }

                                        var fieldDef = new FieldDef(
                                            fieldTagValue,
                                            fieldName,
                                            type,
                                            required,
                                            dataField,
                                            isLog,
                                            dateFormat
                                        );
                                        msgDef.AddChild(fieldDef);
                                    }
                                    else if (childNode.Name == "Loop")
                                    {
                                        string loopTagValue = childNode.Attributes?["Tag"]?.Value ?? "";
                                        string loopName = childNode.Attributes?["Name"]?.Value ?? "Unknown";
                                        string counterValue = childNode.Attributes?["Counter"]?.Value ?? "";
                                        var loopDef = new LoopDef(
                                            loopTagValue,
                                            loopName,
                                            counterValue,
                                            required
                                        );
                                        msgDef.AddChild(loopDef);
                                        specs.Loops.Add(loopDef);

                                        var loopChildNodes = childNode.SelectNodes("Field | Loop");
                                        if (loopChildNodes != null)
                                        {
                                            foreach (XmlNode loopChildNode in loopChildNodes)
                                            {
                                                string loopCommentText = "";
                                                var loopPreviousNode = loopChildNode.PreviousSibling;
                                                while (loopPreviousNode != null && loopPreviousNode.NodeType == XmlNodeType.Comment)
                                                {
                                                    loopCommentText = loopPreviousNode.InnerText + "\n" + loopCommentText;
                                                    loopPreviousNode = loopPreviousNode.PreviousSibling;
                                                }

                                                bool loopRequired = false;
                                                if (!string.IsNullOrEmpty(loopCommentText))
                                                {
                                                    var loopMatch = Regex.Match(loopCommentText, @"Required:\s*(Y|N)", RegexOptions.IgnoreCase);
                                                    if (loopMatch.Success)
                                                    {
                                                        loopRequired = loopMatch.Groups[1].Value.ToUpper() == "Y";
                                                    }
                                                }

                                                if (loopChildNode.Name == "Field")
                                                {
                                                    string loopFieldTagValue = loopChildNode.Attributes?["Tag"]?.Value ?? "";
                                                    string loopFieldName = loopChildNode.Attributes?["Name"]?.Value ?? "Unknown";
                                                    string loopTypeStr = loopChildNode.Attributes?["Type"]?.Value?.Trim() ?? "String"; // Thêm Trim()
                                                    FieldType loopType;
                                                    if (!Enum.TryParse(loopTypeStr, true, out FieldType loopParsedType)) // ignoreCase: true
                                                    {
                                                        Console.WriteLine($"Warning: Unrecognized Type '{loopTypeStr}' for Nested Field Tag={loopFieldTagValue}, Name={loopFieldName}. Defaulting to String.");
                                                        loopType = FieldType.String;
                                                    }
                                                    else
                                                    {
                                                        loopType = loopParsedType;
                                                    }
                                                    string? loopDataField = loopChildNode.Attributes?["DataField"]?.Value;
                                                    bool loopIsLog = bool.TryParse(loopChildNode.Attributes?["IsLog"]?.Value, out bool loopLog) ? loopLog : false;
                                                    string? loopDateFormat = null;

                                                    if (loopType == FieldType.Date && loopChildNode.Attributes?["DateFormat"] != null)
                                                    {
                                                        loopDateFormat = loopChildNode.Attributes?["DateFormat"]?.Value;
                                                    }

                                                    var loopFieldDef = new FieldDef(
                                                        loopFieldTagValue,
                                                        loopFieldName,
                                                        loopType,
                                                        loopRequired,
                                                        loopDataField,
                                                        loopIsLog,
                                                        loopDateFormat
                                                    );
                                                    loopDef.AddChild(loopFieldDef);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"XML Parsing Error: {ex.Message} at Line {ex.LineNumber}, Position {ex.LinePosition}");
                throw;
            }
        }
    }
}