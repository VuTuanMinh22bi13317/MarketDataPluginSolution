namespace MarketDataPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;

    public static class FixParser
    {
        public static void ParseSpecsFromXml(string xmlFilePath, Specs specs)
        {
            if (string.IsNullOrEmpty(xmlFilePath) || !File.Exists(xmlFilePath))
                throw new ArgumentException("Invalid or missing XML file path.", nameof(xmlFilePath));

            string xmlContent = File.ReadAllText(xmlFilePath);
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new InvalidOperationException("XML content is empty.");

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

                        var msgDef = new MsgDef(
                            name: name,
                            msgType: msgType,
                            direction: direction
                        );
                        specs.Messages.Add(msgDef);

                        var childNodes = msgNode.SelectNodes("Field | Loop");
                        if (childNodes != null)
                        {
                            foreach (XmlNode childNode in childNodes)
                            {
                                if (childNode.Name == "Field")
                                {
                                    string fieldTagValue = childNode.Attributes?["Tag"]?.Value ?? "";
                                    var fieldDef = new FieldDef(
                                        tag: fieldTagValue,
                                        name: childNode.Attributes?["Name"]?.Value ?? "Unknown",
                                        type: Enum.TryParse(childNode.Attributes?["Type"]?.Value, out FieldType type) ? type : FieldType.String,
                                        required: bool.TryParse(childNode.Attributes?["Required"]?.Value, out bool required) ? required : false,
                                        dataField: childNode.Attributes?["DataField"]?.Value,
                                        isLog: bool.TryParse(childNode.Attributes?["IsLog"]?.Value, out bool isLog) ? isLog : false
                                    );
                                    msgDef.AddChild(fieldDef);
                                }
                                else if (childNode.Name == "Loop")
                                {
                                    string loopTagValue = childNode.Attributes?["Tag"]?.Value ?? "";
                                    string counterValue = childNode.Attributes?["Counter"]?.Value ?? "";
                                    var loopDef = new LoopDef(
                                        name: childNode.Attributes?["Name"]?.Value ?? "Unknown",
                                        tag: loopTagValue,
                                        counter: counterValue,
                                        required: bool.TryParse(childNode.Attributes?["Required"]?.Value, out bool required) ? required : false
                                    );
                                    msgDef.AddChild(loopDef);
                                    specs.Loops.Add(loopDef);

                                    var loopChildNodes = childNode.SelectNodes("Field | Loop");
                                    if (loopChildNodes != null)
                                    {
                                        foreach (XmlNode loopChildNode in loopChildNodes)
                                        {
                                            if (loopChildNode.Name == "Field")
                                            {
                                                string loopFieldTagValue = loopChildNode.Attributes?["Tag"]?.Value ?? "";
                                                var loopFieldDef = new FieldDef(
                                                    tag: loopFieldTagValue,
                                                    name: loopChildNode.Attributes?["Name"]?.Value ?? "Unknown",
                                                    type: Enum.TryParse(loopChildNode.Attributes?["Type"]?.Value, out FieldType loopFieldType) ? loopFieldType : FieldType.String,
                                                    required: bool.TryParse(loopChildNode.Attributes?["Required"]?.Value, out bool loopFieldRequired) ? loopFieldRequired : false,
                                                    dataField: loopChildNode.Attributes?["DataField"]?.Value,
                                                    isLog: bool.TryParse(loopChildNode.Attributes?["IsLog"]?.Value, out bool loopFieldIsLog) ? loopFieldIsLog : false
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
    }
}