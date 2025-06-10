using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class Msg
    {
        private readonly MsgDef _msgDef;
        private readonly Dictionary<string, MsgDef>? _msgDefs;

        public Msg(MsgDef msgDef, Dictionary<string, MsgDef>? msgDefs)
        {
            _msgDef = msgDef ?? throw new ArgumentNullException(nameof(msgDef));
            _msgDefs = msgDefs ?? throw new ArgumentNullException(nameof(msgDefs));
        }

        public ParseResult Parse(string rawMessage, List<string> errors)
        {
            if (string.IsNullOrEmpty(rawMessage))
            {
                errors.Add("Empty message");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }

            Console.WriteLine($"Debug: Parsing message with MsgType: {_msgDef.MsgType}");

            // Step 1: Validate BeginString
            string expectedBeginString = $"8={Specs.BeginString}{Specs.DelimiterChar}";
            if (!rawMessage.StartsWith(expectedBeginString))
            {
                string actualBeginString = rawMessage.Length >= 8 ? rawMessage.Substring(0, Math.Min(8, rawMessage.IndexOf(Specs.DelimiterChar) != -1 ? rawMessage.IndexOf(Specs.DelimiterChar) : 8)) : rawMessage;
                errors.Add($"Invalid BeginString: Expected '{expectedBeginString}', got '{actualBeginString}'");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), rawMessage, false, 0);
            }

            // Step 2: Extract BodyLength (tag 9)
            int bodyLengthTagStart = rawMessage.IndexOf($"{Specs.DelimiterChar}9=") + 2;
            if (bodyLengthTagStart < 2 || bodyLengthTagStart >= rawMessage.Length)
            {
                errors.Add("Invalid or missing BodyLength (Tag 9)");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), rawMessage, false, 0);
            }
            int bodyLengthTagEnd = rawMessage.IndexOf(Specs.DelimiterChar, bodyLengthTagStart);
            if (bodyLengthTagEnd < 0)
            {
                errors.Add("Missing delimiter after BodyLength (Tag 9)");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), rawMessage, false, 0);
            }
            string bodyLengthStr = rawMessage.Substring(bodyLengthTagStart, bodyLengthTagEnd - bodyLengthTagStart);
            if (!int.TryParse(bodyLengthStr, out int bodyLength))
            {
                errors.Add($"Invalid BodyLength value: '{bodyLengthStr}'");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), rawMessage, false, 0);
            }

            // Step 3: Extract and validate CheckSum (tag 10)
            int checksumStart = rawMessage.LastIndexOf($"{Specs.DelimiterChar}10=");
            bool hasValidChecksum = checksumStart >= 0;
            int expectedChecksum = 0;
            if (hasValidChecksum)
            {
                int checksumEnd = rawMessage.Length;
                string checkSumValueRaw = rawMessage.Substring(checksumStart + 4, Math.Min(3, checksumEnd - (checksumStart + 4))).TrimEnd(Specs.DelimiterChar);
                if (!int.TryParse(checkSumValueRaw, out expectedChecksum))
                {
                    errors.Add($"Invalid CheckSum value: '{checkSumValueRaw}'");
                    hasValidChecksum = false;
                }
            }
            else
            {
                errors.Add("Missing CheckSum (Tag 10)");
            }

            // Step 4: Calculate and validate checksum
            bool checkSumPassed = !Specs.InMsgChecksum || !hasValidChecksum; // Mặc định true nếu không kiểm tra hoặc thiếu checksum
            int calculatedChecksum = 0;
            if (Specs.InMsgChecksum && hasValidChecksum)
            {
                string messageForChecksum = checksumStart > 0 ? rawMessage.Substring(0, checksumStart) : rawMessage;
                calculatedChecksum = CalculateCheckSum(Encoding.ASCII.GetBytes(messageForChecksum));
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
            string[] fieldPairs = rawMessage.Split(Specs.DelimiterChar);

            int currentIndex = 0;
            MsgDef? headerDef = _msgDefs?.GetValueOrDefault("Header");
            if (headerDef == null)
            {
                errors.Add("Header definition not found in configuration");
                return new ParseResult(errors, fields, loops, string.Join(Specs.DelimiterChar.ToString(), unparsedFields), checkSumPassed, calculatedChecksum);
            }

            while (currentIndex < fieldPairs.Length)
            {
                string pair = fieldPairs[currentIndex];
                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);

                    Console.WriteLine($"Debug: Processing pair: {pair}");

                    // Kiểm tra Header
                    if (headerDef.GetChildren().TryGetValue(tag, out object? headerChild))
                    {
                        if (headerChild is FieldDef headerFieldDef)
                        {
                            var field = ParseField(tag, value, headerFieldDef);
                            fields.Add(field);
                            Console.WriteLine($"Debug: Added header field: Tag={tag}, Name={headerFieldDef.Name}, Value={value}");
                            currentIndex++;
                            continue;
                        }
                    }

                    if (tag == "10") // Xử lý CheckSum cuối cùng
                    {
                        if (headerDef.GetChildren().TryGetValue("10", out object? checksumChild))
                        {
                            if (checksumChild is FieldDef checksumFieldDef)
                            {
                                var field = ParseField(tag, value, checksumFieldDef);
                                fields.Add(field);
                                Console.WriteLine($"Debug: Added checksum field: Tag={tag}, Name={checksumFieldDef.Name}, Value={value}");
                            }
                        }
                        break;
                    }

                    // Kiểm tra Body của MsgType
                    if (_msgDef.GetChildren().TryGetValue(tag, out object? bodyChild))
                    {
                        if (bodyChild is FieldDef bodyFieldDef)
                        {
                            var field = ParseField(tag, value, bodyFieldDef);
                            fields.Add(field);
                            Console.WriteLine($"Debug: Added body field: Tag={tag}, Name={bodyFieldDef.Name}, Value={value}");
                        }
                        else if (bodyChild is LoopDef loopDef)
                        {
                            if (pair.StartsWith($"{loopDef.Counter}="))
                            {
                                if (int.TryParse(value, out int loopCount) && loopCount > 0)
                                {
                                    currentIndex++;
                                    for (int loopEntry = 0; loopEntry < loopCount && currentIndex < fieldPairs.Length; loopEntry++)
                                    {
                                        var currentLoop = new Loop(loopDef.Counter, loopDef.Name, value ?? "0");
                                        bool loopStarted = false;
                                        string firstLoopTag = loopDef.GetChildren().Keys.FirstOrDefault();
                                        while (currentIndex < fieldPairs.Length && !loopStarted)
                                        {
                                            if (firstLoopTag != null && fieldPairs[currentIndex].StartsWith($"{firstLoopTag}="))
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
                                                    Console.WriteLine($"Debug: Added loop field: Tag={loopTag}, Name={loopFieldDef.Name}, Value={loopValue}");
                                                }
                                                else
                                                {
                                                    unparsedFields.Add(loopPair);
                                                    Console.WriteLine($"Debug: Unparsed loop field: {loopPair}");
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
                        Console.WriteLine($"Debug: Unparsed field: {pair}");
                    }
                }
                currentIndex++;
            }

            // Check for required fields
            foreach (var child in _msgDef.GetChildren())
            {
                if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                {
                    if (!fields.Exists(f => f.Tag == fieldDef.Tag.ToString()))
                    {
                        errors.Add($"Missing required field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                    }
                }
            }
            foreach (var child in headerDef.GetChildren())
            {
                if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                {
                    if (!fields.Exists(f => f.Tag == fieldDef.Tag.ToString()))
                    {
                        errors.Add($"Missing required header field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                    }
                }
            }

            string unparsedText = unparsedFields.Count > 0 ? string.Join(Specs.DelimiterChar.ToString(), unparsedFields) : string.Empty;
            Console.WriteLine($"Debug: Total fields parsed: {fields.Count}, Unparsed fields: {unparsedFields.Count}");
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
                ParsedValue = parsedValue ?? string.Empty
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