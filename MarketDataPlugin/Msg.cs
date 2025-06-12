using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
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
        public long MsgSeqNum => MsgSeqNumField?.StringValue != null && long.TryParse(MsgSeqNumField.StringValue, out long seq) ? seq : 0;
        public List<string> ParseErrors { get; } = new List<string>(); // List of exceptions/errors

        private const char DELIMITER_CHAR = '\x01';

        private static int _GetMsgLength(string raw, int offset)
        {
            try
            {
                int beginStringIdx = raw.IndexOf("8=" + Specs.BeginString, offset);
                if (beginStringIdx != offset)
                {
                    Console.WriteLine($"Debug: BeginString (Tag 8) mismatch at offset {offset}, found at {beginStringIdx}.");
                    return -1;
                }

                int bodyLengthIdx = raw.IndexOf(DELIMITER_CHAR + "9=", offset);
                if (bodyLengthIdx < 0)
                {
                    Console.WriteLine($"Debug: Missing BodyLength (Tag 9) at offset {offset}.");
                    return -1;
                }
                bodyLengthIdx += 3;
                int nextFieldIdx = raw.IndexOf(DELIMITER_CHAR, bodyLengthIdx);
                if (nextFieldIdx < 0)
                {
                    Console.WriteLine($"Debug: Missing delimiter after BodyLength at offset {bodyLengthIdx}.");
                    return -1;
                }
                string bodyLengthStr = raw.Substring(bodyLengthIdx, nextFieldIdx - bodyLengthIdx);
                if (!int.TryParse(bodyLengthStr, out int bodyLength))
                {
                    Console.WriteLine($"Debug: Invalid BodyLength value '{bodyLengthStr}' at offset {bodyLengthIdx}.");
                    return -1;
                }

                int checkSumIdx = raw.IndexOf(DELIMITER_CHAR + "10=", nextFieldIdx);
                if (checkSumIdx >= 0)
                {
                    int eomIdx = raw.IndexOf(DELIMITER_CHAR, checkSumIdx + 3);
                    if (eomIdx >= 0)
                        return eomIdx - beginStringIdx + 1;
                    Console.WriteLine($"Debug: No end delimiter after CheckSum at index {checkSumIdx}, using body length.");
                }
                else
                {
                    Console.WriteLine($"Debug: No Tag 10 found after body length {bodyLength} at index {nextFieldIdx}, raw snippet: {raw.Substring(nextFieldIdx, Math.Min(50, raw.Length - nextFieldIdx))}");
                    if (Specs.InMsgChecksum)
                    {
                        Console.WriteLine($"Debug: Skipping due to missing required CheckSum.");
                        return -1;
                    }
                }
                return bodyLength + nextFieldIdx - offset + 1; // Fallback to body length + offset if CheckSum is optional
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Debug: Invalid BodyLength format: {ex.Message}");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Failed to calculate message length: {ex.Message}");
                return -1;
            }
        }

        public static int GetMsgLength(string raw, int offset)
        {
            return _GetMsgLength(raw, offset);
        }

        private static string _GetMsgType(string raw)
        {
            try
            {
                int startIdx = raw.IndexOf(DELIMITER_CHAR + "35=");
                if (startIdx < 0)
                {
                    Console.WriteLine("Debug: Missing MsgType (Tag 35) in message.");
                    return null;
                }
                startIdx += 4;
                int endIdx = raw.IndexOf(DELIMITER_CHAR, startIdx);
                if (endIdx < 0)
                {
                    Console.WriteLine("Debug: Missing delimiter after MsgType (Tag 35).");
                    return null;
                }
                return raw.Substring(startIdx, endIdx - startIdx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Failed to extract MsgType: {ex.Message}");
                return null;
            }
        }

        public Msg(MsgDef def, string raw)
        {
            _msgDef = def ?? throw new ArgumentNullException(nameof(def));
            Raw = raw?.Trim() ?? throw new ArgumentNullException(nameof(raw));

            try
            {
                List<TVP> pairs = TVP.FromString(Raw);
                if (pairs.Count == 0)
                {
                    ParseErrors.Add("Debug: No valid tag-value pairs found in message.");
                    return;
                }

                var lastTVP = pairs.Count > 0 ? pairs[pairs.Count - 1] : null;
                if (lastTVP?.Tag == "10")
                {
                    CheckSumField = new Field(_msgDef.CheckSum);
                    CheckSumField = new Field(_msgDef.CheckSum, lastTVP);
                    pairs.RemoveAt(pairs.Count - 1);
                }
                else if (Specs.InMsgChecksum)
                {
                    Console.WriteLine($"Debug: No valid Tag 10 found at end of message, length: {Raw.Length}, snippet: {Raw.Substring(Math.Max(0, Raw.Length - 50))}");
                }

                foreach (var child in _msgDef.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        TVP? tvp = null;
                        for (int i = 0; i < pairs.Count; i++)
                        {
                            if (pairs[i].Tag == fieldDef.Tag.ToString())
                            {
                                tvp = pairs[i];
                                pairs.RemoveAt(i);
                                break;
                            }
                        }
                        var field = new Field(fieldDef, tvp);
                        Fields.Add(field);
                        if (!string.IsNullOrEmpty(fieldDef.DataField))
                            DataFields.Add(field);
                    }
                    else if (child.Value is LoopDef loopDefDef)
                    {
                        Loops.Clear();
                        while (true)
                        {
                            int leftCount = pairs.Count;
                            if (leftCount == 0) break;

                            int firstTag = loopDefDef.Children.First().Value.Tag;
                            int startIdx = 0;
                            for (; startIdx < pairs.Count; startIdx++)
                            {
                                if (pairs[startIdx].Tag == firstTag.ToString()) break;
                            }
                            if (startIdx == pairs.Count) break;

                            var loop = new Loop(this, loopDefDef, pairs, startIdx);
                            if (leftCount != pairs.Count)
                                Loops.Add(loop);
                            else break;
                        }
                    }
                }

                if (pairs.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (TVP tvp in pairs)
                    {
                        if (sb.Length > 0) sb.Append(DELIMITER_CHAR);
                        sb.Append(tvp.Raw);
                    }
                    UnparsedText = sb.ToString();
                    Console.WriteLine($"Debug: Unparsed text remaining: {UnparsedText}");
                }

                _AssignFields();
                if (Specs.InMsgChecksum && CheckSumField != null)
                {
                    int trailerIdx = Raw.IndexOf("10=");
                    string checksumRaw = trailerIdx > 0 ? Raw.Substring(0, trailerIdx) : Raw;
                    CheckSumPassed = CheckSum == _CalculateCheckSum(checksumRaw);
                }
            }
            catch (Exception ex)
            {
                ParseErrors.Add($"Debug: Error processing message data: {ex.Message}");
                throw new InvalidDataException("Error processing message data. All data should be clean at this stage.", ex);
            }
        }

        public ParseResult Parse(string rawMessage, List<string> errors)
        {
            try
            {
                if (string.IsNullOrEmpty(rawMessage))
                {
                    Console.WriteLine("Debug: Empty message received, returning null. Remaining data length: 0");
                    return null;
                }

                string expectedBeginString = $"8={Specs.BeginString}{Specs.DelimiterChar}";
                if (!rawMessage.StartsWith(expectedBeginString))
                    throw new InvalidDataException($"Debug: Invalid BeginString: Expected '{expectedBeginString}', got '{rawMessage.Substring(0, Math.Min(8, rawMessage.IndexOf(Specs.DelimiterChar)))}'");

                int msgLength = GetMsgLength(rawMessage, 0);
                if (msgLength <= 0)
                {
                    Console.WriteLine($"Debug: Invalid MsgLength ({msgLength}) at index 0, skipping segment. Raw snippet: {rawMessage.Substring(0, Math.Min(50, rawMessage.Length))}");
                    return null; // Skip invalid segments
                }
                else if (msgLength > rawMessage.Length)
                {
                    Console.WriteLine($"Debug: MsgLength ({msgLength}) exceeds raw data length {rawMessage.Length}, truncating to {rawMessage.Length}.");
                    msgLength = rawMessage.Length;
                }
                Console.WriteLine($"Debug: Parsed message length: {msgLength} at index 0");

                string msgType = _GetMsgType(rawMessage);
                if (msgType == null)
                {
                    Console.WriteLine("Debug: No MsgType found, skipping segment.");
                    return null;
                }

                if (Specs.MsgDefs == null || !Specs.MsgDefs.TryGetValue(msgType, out MsgDef? def))
                    throw new InvalidDataException($"Debug: No definition found for MsgType: {msgType}");

                var msg = new Msg(def, rawMessage.Substring(0, msgLength));
                errors.AddRange(msg.ParseErrors);
                return msg.Parse(rawMessage.Substring(msgLength), errors);
            }
            catch (Exception ex)
            {
                ParseErrors.Add($"Debug: Error in Parse: {ex.Message}, remaining data: {rawMessage.Length} bytes");
                return null; // Return null on error to continue with next segment
            }
        }

        private void _AssignFields()
        {
            try
            {
                foreach (var field in Fields)
                {
                    if (int.Parse(field.Tag) == 34)
                    {
                        MsgSeqNumField = field;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Debug: Failed to assign fields.", ex);
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
                throw new InvalidOperationException("Debug: Failed to calculate checksum.", ex);
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
                throw new InvalidDataException($"Debug: Failed to parse field {tag} ({fieldDef.Name}).", ex);
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
                    case FieldType.Int: return int.TryParse(value, out int i) ? (object)i : throw new InvalidDataException($"Debug: Invalid integer value: '{value}'");
                    case FieldType.Double: return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (object)d : throw new InvalidDataException($"Debug: Invalid double value: '{value}'");
                    case FieldType.Date:
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
                        throw new InvalidDataException($"Debug: Invalid date value: '{value}'");
                    default: return value;
                }
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException("Debug: Failed to parse field value due to format error.", ex);
            }
        }

        public int CheckSum => CheckSumField?.StringValue != null && int.TryParse(CheckSumField.StringValue, out int cs) ? cs : throw new InvalidDataException("Debug: CheckSum value is invalid or missing.");
    }

    public class InvalidDataException : Exception
    {
        public InvalidDataException(string message) : base(message) { }
        public InvalidDataException(string message, Exception inner) : base(message, inner) { }
    }
}