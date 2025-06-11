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
        public string Raw { get; }
        public MsgDef Def => _msgDef ?? throw new InvalidOperationException("MsgDef is not initialized.");
        public List<Field> Fields { get; } = new List<Field>();
        public List<Field> DataFields { get; } = new List<Field>();
        public List<Loop> Loops { get; } = new List<Loop>();
        public Field? CheckSumField { get; set; }
        public Field? MsgSeqNumField { get; set; }
        public string UnparsedText { get; set; } = string.Empty;
        public bool CheckSumPassed { get; set; } = true;
        public long MsgSeqNum => MsgSeqNumField?.Value != null && long.TryParse(MsgSeqNumField.Value.ToString(), out long seq) ? seq : 0;

        private const char DELIMITER_CHAR = '\x01';

        static int _GetMsgLength(string raw, int offset)
        {
            try
            {
                int bodyLengthIdx = raw.IndexOf(DELIMITER_CHAR + "9=", offset);
                if (bodyLengthIdx < 0) throw new InvalidDataException("Missing BodyLength (Tag 9) in message.");
                bodyLengthIdx += 3;
                int nextFieldIdx = raw.IndexOf(DELIMITER_CHAR, bodyLengthIdx);
                if (nextFieldIdx < 0) throw new InvalidDataException("Missing delimiter after BodyLength (Tag 9).");
                string bodyLength = raw.Substring(bodyLengthIdx, nextFieldIdx - bodyLengthIdx);
                return int.Parse(bodyLength) + nextFieldIdx - offset + 1 + 7;
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Invalid BodyLength value format.", ex);
            }
        }

        public static int GetMsgLength(string raw, int offset)
        {
            try
            {
                int beginStringIdx = raw.IndexOf("8=" + Specs.BeginString, offset);
                if (beginStringIdx != offset) throw new InvalidDataException($"BeginString (Tag 8) must start at offset {offset}, found at {beginStringIdx}.");
                int checkSumIdx = raw.IndexOf(DELIMITER_CHAR + "10=", beginStringIdx + 3);
                if (checkSumIdx < 0) return _GetMsgLength(raw, offset);
                int eomIdx = raw.IndexOf(DELIMITER_CHAR, checkSumIdx + 3);
                if (eomIdx < 0) return _GetMsgLength(raw, offset);
                return eomIdx - beginStringIdx + 1;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to calculate message length.", ex);
            }
        }

        static string _GetMsgType(string raw)
        {
            try
            {
                int startIdx = raw.IndexOf(DELIMITER_CHAR + "35=");
                if (startIdx < 0) throw new InvalidDataException("Missing MsgType (Tag 35) in message.");
                startIdx += 4;
                int endIdx = raw.IndexOf(DELIMITER_CHAR, startIdx);
                if (endIdx < 0) throw new InvalidDataException("Missing delimiter after MsgType (Tag 35).");
                return raw.Substring(startIdx, endIdx - startIdx);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Failed to extract MsgType.", ex);
            }
        }

        public Msg(MsgDef def, string raw)
        {
            _msgDef = def ?? throw new ArgumentNullException(nameof(def));
            Raw = raw?.Trim() ?? throw new ArgumentNullException(nameof(raw));
            try
            {
                List<TVP> pairs = TVP.FromString(Raw) ?? throw new InvalidDataException("Failed to parse raw message into TVP pairs.");
                if (pairs.Count == 0) throw new InvalidDataException("No valid tag-value pairs found in message.");

                var lastTVP = pairs[pairs.Count - 1];
                if (lastTVP.Tag == "10")
                {
                    CheckSumField = new Field("10", "CheckSum", FieldType.Int, true) { Value = lastTVP.Value };
                    pairs.RemoveAt(pairs.Count - 1);
                }

                foreach (MsgChildDef childDef in _msgDef.Children)
                {
                    if (childDef.GetType() == typeof(LoopDef))
                    {
                        var loopDef = (LoopDef?)_msgDef.GetChildren().Values.FirstOrDefault(v => v == childDef && v.GetType() == typeof(LoopDef));
                        if (loopDef != null)
                        {
                            Loops.Clear();
                            while (pairs.Count > 0)
                            {
                                int startIdx = 0;
                                for (; startIdx < pairs.Count; startIdx++)
                                {
                                    if (loopDef.Children.ContainsKey(pairs[startIdx].Tag)) break;
                                }
                                if (startIdx == pairs.Count) break;
                                var loop = new Loop(loopDef.Counter ?? "0", loopDef.Name, pairs, startIdx);
                                if (loop.ParsedFields.Count > 0) Loops.Add(loop);
                                pairs.RemoveRange(0, startIdx + loop.ParsedFields.Count);
                            }
                        }
                    }
                    else if (childDef.GetType() == typeof(FieldDef))
                    {
                        var fieldDef = (FieldDef)childDef;
                        var tvp = pairs.Find(p => p.Tag == fieldDef.Tag.ToString());
                        if (tvp != null)
                        {
                            pairs.Remove(tvp);
                            var field = new Field(fieldDef.Tag.ToString(), fieldDef.Name, fieldDef.Type, fieldDef.IsRequired)
                            {
                                Value = tvp.Value ?? fieldDef.DefaultValue
                            };
                            Fields.Add(field);
                            if (!string.IsNullOrEmpty(field.Value)) DataFields.Add(field);
                        }
                    }
                }

                if (pairs.Count > 0)
                {
                    UnparsedText = string.Join(DELIMITER_CHAR.ToString(), pairs.Select(p => p.Raw));
                }

                _AssignFields();
                if (Specs.InMsgChecksum)
                {
                    int trailerIdx = Raw.IndexOf("10=");
                    string checksumRaw = trailerIdx > 0 ? Raw.Substring(0, trailerIdx) : Raw;
                    CheckSumPassed = CheckSum == _CalculateCheckSum(checksumRaw);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Error processing message data. All data should be clean at this stage.", ex);
            }
        }

        public ParseResult Parse(string rawMessage, List<string> errors)
        {
            try
            {
                if (string.IsNullOrEmpty(rawMessage)) throw new InvalidDataException("Empty message received.");

                string expectedBeginString = $"8={Specs.BeginString}{Specs.DelimiterChar}";
                if (!rawMessage.StartsWith(expectedBeginString))
                    throw new InvalidDataException($"Invalid BeginString: Expected '{expectedBeginString}', got '{rawMessage.Substring(0, Math.Min(8, rawMessage.IndexOf(Specs.DelimiterChar)))}'");

                int bodyLengthTagStart = rawMessage.IndexOf($"{Specs.DelimiterChar}9=") + 2;
                if (bodyLengthTagStart < 2 || bodyLengthTagStart >= rawMessage.Length)
                    throw new InvalidDataException("Invalid or missing BodyLength (Tag 9).");
                int bodyLengthTagEnd = rawMessage.IndexOf(Specs.DelimiterChar, bodyLengthTagStart);
                if (bodyLengthTagEnd < 0) throw new InvalidDataException("Missing delimiter after BodyLength (Tag 9).");
                string bodyLengthStr = rawMessage.Substring(bodyLengthTagStart, bodyLengthTagEnd - bodyLengthTagStart);
                if (!int.TryParse(bodyLengthStr, out int bodyLength))
                    throw new InvalidDataException($"Invalid BodyLength value: '{bodyLengthStr}'");

                int checksumStart = rawMessage.LastIndexOf($"{Specs.DelimiterChar}10=");
                if (checksumStart < 0) throw new InvalidDataException("Missing CheckSum (Tag 10).");
                int checksumEnd = rawMessage.Length;
                string checkSumValueRaw = rawMessage.Substring(checksumStart + 4, Math.Min(3, checksumEnd - (checksumStart + 4))).TrimEnd(Specs.DelimiterChar);
                if (!int.TryParse(checkSumValueRaw, out int expectedChecksum))
                    throw new InvalidDataException($"Invalid CheckSum value: '{checkSumValueRaw}'");

                bool checkSumPassed = !Specs.InMsgChecksum;
                int calculatedChecksum = 0;
                if (Specs.InMsgChecksum)
                {
                    string messageForChecksum = rawMessage.Substring(0, checksumStart);
                    calculatedChecksum = _CalculateCheckSum(messageForChecksum);
                    checkSumPassed = (calculatedChecksum % 256 == expectedChecksum);
                    if (!checkSumPassed) throw new InvalidDataException($"Checksum mismatch: Calculated {calculatedChecksum % 256}, Expected {expectedChecksum}");
                }

                var fields = new List<MessageField>();
                var loops = new List<Loop>();
                var unparsedFields = new List<string>();
                string[] fieldPairs = rawMessage.Split(Specs.DelimiterChar);

                int currentIndex = 0;

                while (currentIndex < fieldPairs.Length)
                {
                    string pair = fieldPairs[currentIndex];
                    int equalsIndex = pair.IndexOf('=');
                    if (equalsIndex <= 0) throw new InvalidDataException($"Invalid field pair format at index {currentIndex}: '{pair}'");

                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);

                    if (tag == "10" && _msgDef.CheckSum != null)
                    {
                        fields.Add(ParseField(tag, value, _msgDef.CheckSum));
                        break;
                    }

                    if (_msgDef.GetChildren().TryGetValue(tag, out object? bodyChild))
                    {
                        if (bodyChild is FieldDef bodyFieldDef)
                        {
                            fields.Add(ParseField(tag, value, bodyFieldDef));
                        }
                        else if (bodyChild is LoopDef loopDef)
                        {
                            if (pair.StartsWith($"{loopDef.Counter}="))
                            {
                                if (!int.TryParse(value, out int loopCount) || loopCount <= 0)
                                    throw new InvalidDataException($"Invalid loop count value: '{value}' for counter {loopDef.Counter}");
                                currentIndex++;
                                for (int loopEntry = 0; loopEntry < loopCount && currentIndex < fieldPairs.Length; loopEntry++)
                                {
                                    var currentLoop = new Loop(loopDef.Counter ?? "0", loopDef.Name, value);
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
                                        if (loopEqualsIndex <= 0) throw new InvalidDataException($"Invalid loop field pair format: '{loopPair}'");
                                        string loopTag = loopPair.Substring(0, loopEqualsIndex);
                                        string loopValue = loopPair.Substring(loopEqualsIndex + 1);
                                        if (loopTag == "10") break;
                                        if (loopDef.GetChildren().TryGetValue(loopTag, out FieldDef? loopFieldDef))
                                        {
                                            currentLoop.AddParsedField(ParseField(loopTag, loopValue, loopFieldDef));
                                        }
                                        else
                                        {
                                            unparsedFields.Add(loopPair);
                                        }
                                        currentIndex++;
                                    }
                                    if (currentLoop.ParsedFields.Count > 0) loops.Add(currentLoop);
                                }
                                continue;
                            }
                        }
                    }
                    else
                    {
                        unparsedFields.Add(pair);
                    }
                    currentIndex++;
                }

                foreach (var child in _msgDef.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                    {
                        if (!fields.Exists(f => f.Tag == fieldDef.Tag.ToString()))
                            throw new InvalidDataException($"Missing required field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                    }
                }

                string unparsedText = unparsedFields.Count > 0 ? string.Join(Specs.DelimiterChar.ToString(), unparsedFields) : string.Empty;
                return new ParseResult(errors, fields, loops, unparsedText, checkSumPassed, calculatedChecksum);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Error parsing message. All data should be clean at this stage.", ex);
            }
        }

        private void _AssignFields()
        {
            try
            {
                foreach (var field in Fields)
                {
                    if (field.Tag == "34") // Assuming Tag 34 is for MsgSeqNum
                    {
                        MsgSeqNumField = field;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to assign fields.", ex);
            }
        }

        private int _CalculateCheckSum(string raw)
        {
            try
            {
                int checksum = 0;
                foreach (byte b in Encoding.ASCII.GetBytes(raw))
                {
                    checksum += b;
                }
                return checksum % 256;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to calculate checksum.", ex);
            }
        }

        private MessageField ParseField(string tag, string value, FieldDef fieldDef)
        {
            try
            {
                object? parsedValue = ParseFieldValue(value, fieldDef.Type, fieldDef.DateFormat, fieldDef.TimeZoneEffected, fieldDef.DefaultValue);
                return new MessageField
                {
                    Tag = tag,
                    Name = fieldDef.Name,
                    Value = value ?? fieldDef.DefaultValue,
                    Type = fieldDef.Type.ToString(),
                    DataField = fieldDef.DataField,
                    ParsedValue = parsedValue ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse field {tag} ({fieldDef.Name}).", ex);
            }
        }

        private object? ParseFieldValue(string value, FieldType type, string dateFormat, bool timeZoneEffected, string defaultValue)
        {
            try
            {
                value = value ?? defaultValue;
                if (string.IsNullOrEmpty(value)) return string.Empty;
                switch (type)
                {
                    case FieldType.Int: return int.TryParse(value, out int i) ? (object)i : throw new InvalidDataException($"Invalid integer value: '{value}'");
                    case FieldType.Double: return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (object)d : throw new InvalidDataException($"Invalid double value: '{value}'");
                    case FieldType.Date:
                        if (!string.IsNullOrEmpty(dateFormat))
                        {
                            DateTime dt = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                            if (timeZoneEffected) dt = dt.AddHours(7); // Using TimeZone property if needed
                            return dt;
                        }
                        else if (DateTime.TryParse(value, out DateTime dtDefault))
                        {
                            if (timeZoneEffected) dtDefault = dtDefault.AddHours(7);
                            return dtDefault;
                        }
                        throw new InvalidDataException($"Invalid date value: '{value}'");
                    default: return value;
                }
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Failed to parse field value due to format error.", ex);
            }
        }

        public int CheckSum => CheckSumField?.Value != null && int.TryParse(CheckSumField.Value.ToString(), out int cs) ? cs : throw new InvalidDataException("CheckSum value is invalid or missing.");
    }

    public class InvalidDataException : Exception
    {
        public InvalidDataException(string message) : base(message) { }
        public InvalidDataException(string message, Exception inner) : base(message, inner) { }
    }
}