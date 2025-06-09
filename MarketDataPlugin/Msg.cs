using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text; // Added for Encoding
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Msg
    {
        private readonly FixMessageParser parser;

        public Msg(FixMessageParser parser)
        {
            this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public ParseResult? Parse(string rawMessage, List<string> errors)
        {
            if (string.IsNullOrEmpty(rawMessage))
            {
                errors.Add("Empty message");
                return null;
            }

            Console.WriteLine($"Debug: Raw message: {rawMessage}");

            // Step 1: Validate BeginString (tag 8)
            if (!rawMessage.StartsWith($"8={parser.BeginString}{parser.DelimiterChar}"))
            {
                errors.Add($"Invalid BeginString: Expected '8={parser.BeginString}', got '{rawMessage.Substring(0, Math.Min(10, rawMessage.Length))}'");
                return null;
            }

            // Step 2: Extract BodyLength (tag 9)
            int bodyLengthTagStart = rawMessage.IndexOf($"{parser.DelimiterChar}9=") + 2;
            if (bodyLengthTagStart < 2 || bodyLengthTagStart >= rawMessage.Length)
            {
                errors.Add("Invalid or missing BodyLength (Tag 9)");
                return null;
            }
            int bodyLengthTagEnd = rawMessage.IndexOf(parser.DelimiterChar, bodyLengthTagStart);
            if (bodyLengthTagEnd < 0)
            {
                errors.Add("Missing delimiter after BodyLength (Tag 9)");
                return null;
            }
            string bodyLengthStr = rawMessage.Substring(bodyLengthTagStart, bodyLengthTagEnd - bodyLengthTagStart);
            if (!int.TryParse(bodyLengthStr, out int bodyLength))
            {
                errors.Add($"Invalid BodyLength value: '{bodyLengthStr}'");
                return null;
            }
            Console.WriteLine($"Debug: Found BodyLength at index {bodyLengthTagStart}: {bodyLength}");

            // Step 3: Find CheckSum (tag 10) position
            int checksumStart = rawMessage.IndexOf($"{parser.DelimiterChar}10=", bodyLengthTagEnd);
            if (checksumStart < 0)
            {
                errors.Add("Missing CheckSum (Tag 10)");
                return null;
            }
            int checksumEnd = rawMessage.IndexOf(parser.DelimiterChar, checksumStart + 4);
            if (checksumEnd < 0) checksumEnd = rawMessage.Length;
            string checkSumValueRaw = rawMessage.Substring(checksumStart + 4, Math.Min(3, checksumEnd - (checksumStart + 4))).TrimEnd(parser.DelimiterChar); // Giới hạn 3 ký tự và loại bỏ SOH
            Console.WriteLine($"Debug: Extracted CheckSum raw value: '{checkSumValueRaw}' from index {checksumStart + 4} to {checksumEnd}");

            // Validate checksum value
            if (string.IsNullOrEmpty(checkSumValueRaw) || !checkSumValueRaw.All(char.IsDigit))
            {
                errors.Add($"Invalid CheckSum (Tag 10) value: '{checkSumValueRaw}' (expected numeric)");
                return null;
            }

            // Step 4: Validate checksum if enabled
            bool checkSumPassed = !parser.InMsgChecksum;
            int calculated = 0;
            string expectedCheckSum = checkSumValueRaw.PadLeft(3, '0'); // Padding về 3 chữ số
            if (parser.InMsgChecksum)
            {
                int checksumTagStart = rawMessage.IndexOf($"{parser.DelimiterChar}10=", 0);
                if (checksumTagStart >= 0)
                {
                    string messageForChecksum = rawMessage.Substring(0, checksumTagStart); // Chỉ lấy đến trước tag 10
                    byte[] messageBytes = Encoding.ASCII.GetBytes(messageForChecksum);
                    calculated = FixMessageParser.CalculateCheckSum(messageBytes);
                    Console.WriteLine($"Debug: Checksum message bytes (length {messageBytes.Length}): {BitConverter.ToString(messageBytes)}");
                    Console.WriteLine($"Debug: Expected CheckSum (padded): {expectedCheckSum}");

                    int checkSum = int.Parse(expectedCheckSum);
                    checkSumPassed = (calculated % 256 == checkSum);
                    if (!checkSumPassed)
                    {
                        errors.Add($"Checksum mismatch: Calculated {calculated % 256}, Expected {checkSum}");
                    }
                }
            }

            // Step 5: Look up MsgType in specs
            string? msgType = GetMsgType(rawMessage);
            if (string.IsNullOrEmpty(msgType))
            {
                errors.Add("Invalid MsgType: Could not extract MsgType (tag 35)");
                return null;
            }
            if (!parser.MsgDefs.TryGetValue(msgType, out MsgDef? msgDef))
            {
                errors.Add($"Invalid MsgType: '{msgType}' not found in message definitions");
                return null;
            }

            // Step 6: Parse the message into fields and loops
            var fields = new List<MessageField>();
            var loops = new List<Loop>();
            var unparsedFields = new List<string>();
            string[] fieldPairs = rawMessage.Split(parser.DelimiterChar);
            int currentIndex = 0;

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
                        break;
                    }

                    if (parser.HeaderFields.TryGetValue(tag, out FieldDef? headerField))
                    {
                        var field = ParseField(tag, value, headerField, msgType);
                        fields.Add(field);
                    }
                    else if (msgDef.GetChildren().TryGetValue(tag, out object? childObj))
                    {
                        if (childObj is FieldDef fieldDef)
                        {
                            var field = ParseField(tag, value, fieldDef, msgType);
                            fields.Add(field);
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

                                                if (loopDef.GetChildren().TryGetValue(loopTag, out FieldDef? loopFieldDef))
                                                {
                                                    var field = ParseField(loopTag, loopValue, loopFieldDef, msgType);
                                                    currentLoop.AddParsedField(field);
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
                    else
                    {
                        unparsedFields.Add(pair);
                    }
                }
                currentIndex++;
            }

            // Check for required fields
            foreach (var child in msgDef.GetChildren())
            {
                if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                {
                    if (!fields.Exists(f => f.Tag == fieldDef.Tag.ToString()))
                    {
                        errors.Add($"Missing required field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                        return null;
                    }
                }
            }

            string unparsedText = unparsedFields.Count > 0 ? string.Join(parser.DelimiterChar.ToString(), unparsedFields) : string.Empty;
            return new ParseResult(errors, fields, loops, unparsedText, checkSumPassed, calculated);
        }

        private MessageField ParseField(string tag, string value, FieldDef fieldDef, string msgType)
        {
            string? dateFormat = null; // Initialize with null
            if (fieldDef.Type == FieldType.Date && parser.DateFormats.TryGetValue($"{msgType}:{tag}", out string? format))
            {
                dateFormat = format;
            }
            object? parsedValue = ParseFieldValue(value, fieldDef.Type, dateFormat, fieldDef.TimeZoneEffected);
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

        private object? ParseFieldValue(string value, FieldType type, string? dateFormat, bool timeZoneEffected)
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

        public string? GetMsgType(string message)
        {
            string[] parts = message.Split(parser.DelimiterChar);
            foreach (var part in parts)
            {
                if (part.StartsWith("35=")) return part.Substring(3);
            }
            return null;
        }

        private readonly Dictionary<string, object> loopFieldsDict = new Dictionary<string, object>();
    }
}