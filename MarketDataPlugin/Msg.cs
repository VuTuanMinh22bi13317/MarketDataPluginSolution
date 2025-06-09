using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Msg
    {
        private readonly MsgDef msgDef;
        private readonly char delimiterChar = '\x01';
        private readonly bool inMsgChecksum;

        public Msg(MsgDef msgDef, bool inMsgChecksum = true)
        {
            this.msgDef = msgDef ?? throw new ArgumentNullException(nameof(msgDef));
            this.inMsgChecksum = inMsgChecksum;
        }

        public ParseResult? Parse(string rawMessage, List<string> errors)
        {
            if (string.IsNullOrEmpty(rawMessage))
            {
                errors.Add("Empty message");
                return null;
            }

            Console.WriteLine($"Debug: Parsing message with MsgType: {msgDef.MsgType}");

            // Step 1: Validate BeginString
            if (!rawMessage.StartsWith($"8={msgDef.MsgType.Split('.')[0]}{delimiterChar}"))
            {
                errors.Add($"Invalid BeginString: Expected '8=FIX', got '{rawMessage.Substring(0, Math.Min(5, rawMessage.Length))}'");
                return null;
            }

            // Step 2: Extract BodyLength (tag 9)
            int bodyLengthTagStart = rawMessage.IndexOf($"{delimiterChar}9=") + 2;
            if (bodyLengthTagStart < 2 || bodyLengthTagStart >= rawMessage.Length)
            {
                errors.Add("Invalid or missing BodyLength (Tag 9)");
                return null;
            }
            int bodyLengthTagEnd = rawMessage.IndexOf(delimiterChar, bodyLengthTagStart);
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

            // Step 3: Find CheckSum (tag 10)
            int checksumStart = rawMessage.LastIndexOf($"{delimiterChar}10=");
            if (checksumStart < 0)
            {
                errors.Add("Missing CheckSum (Tag 10)");
                return null;
            }
            int checksumEnd = rawMessage.Length;
            string checkSumValueRaw = rawMessage.Substring(checksumStart + 4, Math.Min(3, checksumEnd - (checksumStart + 4))).TrimEnd(delimiterChar);
            if (!int.TryParse(checkSumValueRaw, out int expectedChecksum))
            {
                errors.Add($"Invalid CheckSum value: '{checkSumValueRaw}'");
                return null;
            }

            // Step 4: Validate checksum if enabled
            bool checkSumPassed = !inMsgChecksum;
            int calculatedChecksum = 0;
            if (inMsgChecksum)
            {
                string messageForChecksum = rawMessage.Substring(0, checksumStart);
                byte[] messageBytes = Encoding.ASCII.GetBytes(messageForChecksum);
                calculatedChecksum = CalculateCheckSum(messageBytes);
                checkSumPassed = (calculatedChecksum % 256 == expectedChecksum);
                if (!checkSumPassed)
                {
                    Console.WriteLine($"Debug: Checksum mismatch: Calculated {calculatedChecksum % 256}, Expected {expectedChecksum}");
                }
            }

            // Step 5: Parse fields and loops
            var fields = new List<MessageField>();
            var loops = new List<Loop>();
            var unparsedFields = new List<string>();
            string[] fieldPairs = rawMessage.Split(delimiterChar);

            int currentIndex = 0;
            while (currentIndex < fieldPairs.Length)
            {
                string pair = fieldPairs[currentIndex];
                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);

                    if (tag == "10") // Kết thúc tại checksum
                    {
                        fields.Add(new MessageField { Tag = tag, Name = "CheckSum", Value = value, Type = "String" });
                        break;
                    }

                    if (msgDef.GetChildren().TryGetValue(tag, out object? child))
                    {
                        if (child is FieldDef fieldDef)
                        {
                            var field = ParseField(tag, value, fieldDef);
                            fields.Add(field);
                        }
                        else if (child is LoopDef loopDef)
                        {
                            if (pair.StartsWith($"{loopDef.Counter}="))
                            {
                                if (int.TryParse(value, out int loopCount) && loopCount > 0)
                                {
                                    currentIndex++;
                                    for (int loopEntry = 0; loopEntry < loopCount && currentIndex < fieldPairs.Length; loopEntry++)
                                    {
                                        var currentLoop = new Loop(loopDef.Counter, loopDef.Name, ""); // Loại bỏ LoopTag, thay bằng chuỗi rỗng
                                        bool loopStarted = false;
                                        while (currentIndex < fieldPairs.Length && !loopStarted)
                                        {
                                            // Tìm điểm bắt đầu loop dựa trên tag đầu tiên của loop (cần điều chỉnh logic nếu có)
                                            if (loopDef.GetChildren().ContainsKey(fieldPairs[currentIndex].Split('=')[0]))
                                            {
                                                loopStarted = true;
                                                break;
                                            }
                                            currentIndex++;
                                        }

                                        if (!loopStarted) break;

                                        while (currentIndex < fieldPairs.Length)
                                        {
                                            string loopPair = fieldPairs[currentIndex];
                                            int loopEqualsIndex = loopPair.IndexOf('=');
                                            if (loopEqualsIndex > 0)
                                            {
                                                string loopTag = loopPair.Substring(0, loopEqualsIndex);
                                                string loopValue = loopPair.Substring(loopEqualsIndex + 1);

                                                if (loopTag == "10") break;

                                                if (loopDef.GetChildren().TryGetValue(loopTag, out FieldDef? loopFieldDef))
                                                {
                                                    var field = ParseField(loopTag, loopValue, loopFieldDef);
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

            string unparsedText = unparsedFields.Count > 0 ? string.Join(delimiterChar.ToString(), unparsedFields) : string.Empty;
            return new ParseResult(errors, fields, loops, unparsedText, checkSumPassed, calculatedChecksum);
        }

        private MessageField ParseField(string tag, string value, FieldDef fieldDef)
        {
            string? dateFormat = fieldDef.Type == FieldType.Date ? fieldDef.DateFormat : null;
            object? parsedValue = ParseFieldValue(value, fieldDef.Type, dateFormat, fieldDef.TimeZoneEffected);
            return new MessageField
            {
                Tag = tag,
                Name = fieldDef.Name,
                Value = value,
                Type = fieldDef.Type.ToString(),
                DataField = fieldDef.DataField,
                ParsedValue = parsedValue ?? string.Empty // Xử lý null
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
                            DateTime dt = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                            if (timeZoneEffected) dt = dt.AddHours(7);
                            return dt;
                        }
                        else if (DateTime.TryParse(value, out DateTime dtDefault))
                        {
                            if (timeZoneEffected) dtDefault = dtDefault.AddHours(7);
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

        private int CalculateCheckSum(byte[] messageBytes)
        {
            int checksum = 0;
            foreach (byte b in messageBytes)
            {
                checksum += b;
            }
            return checksum % 256;
        }
    }
}