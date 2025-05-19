namespace MarketDataPlugin
{
    public class FixParser
    {
        public static void ParseSpecsFromXml(string xmlContent, Specs specs) // Added static
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xmlContent);

            var specsNode = doc.SelectSingleNode("/Specs");
            if (specsNode != null)
            {
                specs.InMsgChecksum = bool.Parse(specsNode.Attributes?["InMsgChecksum"]?.Value ?? "false");
                specs.OutMsgChecksum = bool.Parse(specsNode.Attributes?["OutMsgChecksum"]?.Value ?? "true");
                specs.TimeZone = int.Parse(specsNode.Attributes?["TimeZone"]?.Value ?? "7");

                var msgNodes = specsNode.SelectNodes("Msg");
                if (msgNodes != null)
                {
                    foreach (System.Xml.XmlNode msgNode in msgNodes)
                    {
                        var msgDef = new MsgDef(
                            tag: 35,
                            name: msgNode.Attributes?["Name"]?.Value ?? "Unknown",
                            msgType: msgNode.Attributes?["MsgType"]?.Value ?? "Unknown",
                            direction: specs.GetDirection(msgNode.Attributes?["Direction"]?.Value ?? "In")
                        );
                        specs.Messages.Add(msgDef);

                        var childNodes = msgNode.SelectNodes("Field | Loop");
                        if (childNodes != null)
                        {
                            foreach (System.Xml.XmlNode childNode in childNodes)
                            {
                                if (childNode.Name == "Field")
                                {
                                    var fieldDef = new FieldDef(
                                        tag: int.Parse(childNode.Attributes?["Tag"]?.Value ?? "0"),
                                        name: childNode.Attributes?["Name"]?.Value ?? "Unknown",
                                        type: (FieldType)Enum.Parse(typeof(FieldType), childNode.Attributes?["Type"]?.Value ?? "String"),
                                        required: bool.Parse(childNode.Attributes?["Required"]?.Value ?? "false")
                                    );
                                    msgDef.AddChild(fieldDef);
                                }
                                else if (childNode.Name == "Loop")
                                {
                                    var loopDef = new LoopDef(
                                        name: childNode.Attributes?["Name"]?.Value ?? "Unknown",
                                        tag: int.Parse(childNode.Attributes?["Tag"]?.Value ?? "0"),
                                        counter: int.Parse(childNode.Attributes?["Counter"]?.Value ?? "0"),
                                        required: bool.Parse(childNode.Attributes?["Required"]?.Value ?? "false")
                                    );
                                    msgDef.AddChild(loopDef);
                                }
                            }
                        }
                    }
                }
            }
        }

        public string? GetAttribute(System.Xml.XmlNode node, string name)
        {
            return node?.Attributes?[name]?.Value;
        }
    }
}