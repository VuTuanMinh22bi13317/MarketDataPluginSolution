using System;
using System.Collections.Generic;
using System.Text;
using MarketDataPlugin;
using MarketDataPlugin.DEF;
using System.Globalization;

namespace MarketDataPlugin
{
    public class Msg
    {
        private readonly Specs specs;
        private readonly Dictionary<string, MsgDef> msgDefs;
        private readonly Dictionary<string, string> dateFormats;

        public Msg(Specs specs, Dictionary<string, MsgDef> msgDefs, Dictionary<string, string> dateFormats)
        {
            this.specs = specs ?? throw new ArgumentNullException(nameof(specs));
            this.msgDefs = msgDefs ?? throw new ArgumentNullException(nameof(msgDefs));
            this.dateFormats = dateFormats ?? throw new ArgumentNullException(nameof(dateFormats));
        }

        public ParseResult Parse(string rawMessage)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(rawMessage))
            {
                errors.Add("Empty message");
                return new ParseResult(errors, null);
            }

            var fields = new List<MessageField>();
            var loops = new List<List<MessageField>>();
            var unparsedFields = new List<string>();
            int messageLength = rawMessage.Length;

            if (!rawMessage.StartsWith($"8={specs.BeginString}" + specs.DelimiterChar))
            {
                errors.Add($"Invalid BeginString: Expected '8={specs.BeginString}', got '{rawMessage.Substring(0, Math.Min(10, rawMessage.Length))}'");
                return new ParseResult(errors, null);
            }

            string? msgType = GetMsgType(rawMessage);
            if (string.IsNullOrEmpty(msgType))
            {
                errors.Add("Invalid MsgType: Could not extract MsgType (tag 35)");
                return new ParseResult(errors, null);
            }

            if (!msgDefs.TryGetValue(msgType, out MsgDef? msgDef))
            {
                errors.Add($"Invalid MsgType: '{msgType}' not found in message definitions");
                return new ParseResult(errors, null);
            }

            string[] fieldPairs = rawMessage.Split(specs.DelimiterChar);
            bool checkSumPassed = !specs.InMsgChecksum || VerifyCheckSum(rawMessage, fieldPairs);

            // Parse header and body fields
            for (int i = 0; i < fieldPairs.Length; i++)
            {
                string pair = fieldPairs[i];
                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string tag = pair.Substring(0, equalsIndex);
                    string value = pair.Substring(equalsIndex + 1);
                    if (specs.HeaderFields.TryGetValue(tag, out FieldDef? headerField))
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
                        // We'll handle loops separately below
                    }
                    else if (tag == "10")
                    {
                        // Trailer field, no action needed
                    }
                    else
                    {
                        unparsedFields.Add(pair);
                    }
                }
            }

            // Parse loops
            var loopFieldsDict = new Dictionary<string, object>();
            foreach (var child in msgDef.GetChildren())
            {
                if (child.Value is LoopDef loopDef)
                {
                    string counterTag = loopDef.Counter;
                    int counterIndex = Array.FindIndex(fieldPairs, p => p.StartsWith($"{counterTag}="));
                    if (counterIndex >= 0)
                    {
                        string counterPair = fieldPairs[counterIndex];
                        string counterValue = counterPair.Substring(counterPair.IndexOf('=') + 1);
                        if (int.TryParse(counterValue, out int loopCount))
                        {
                            int currentIndex = counterIndex + 1;
                            for (int loopEntry = 0; loopEntry < loopCount && currentIndex < fieldPairs.Length; loopEntry++)
                            {
                                var currentLoop = new List<MessageField>();
                                bool loopEnded = false;
                                while (currentIndex < fieldPairs.Length && !loopEnded)
                                {
                                    string pair = fieldPairs[currentIndex];
                                    int equalsIndex = pair.IndexOf('=');
                                    if (equalsIndex > 0)
                                    {
                                        string tag = pair.Substring(0, equalsIndex);
                                        string value = pair.Substring(equalsIndex + 1);
                                        if (loopDef.GetChildren().TryGetValue(tag, out object? fieldDefObj) && fieldDefObj is FieldDef fieldDef)
                                        {
                                            var field = ParseField(tag, value, fieldDef, msgType);
                                            currentLoop.Add(field);
                                            if (field.Name == "MarketId" || field.Name == "Symbol" || field.Name == "TransactTime")
                                            {
#pragma warning disable CS8601
                                                loopFieldsDict[field.Name] = field.ParsedValue ?? (object)string.Empty;
#pragma warning restore CS8601
                                            }
                                            currentIndex++;
                                        }
                                        else
                                        {
                                            bool isAnotherCounter = msgDef.GetChildren().Values.OfType<LoopDef>().Any(ld => ld.Counter == tag);
                                            bool isKnownField = msgDef.GetChildren().ContainsKey(tag) || specs.HeaderFields.ContainsKey(tag) || tag == "10";
                                            if (isAnotherCounter || isKnownField)
                                            {
                                                loopEnded = true;
                                            }
                                            else
                                            {
                                                unparsedFields.Add(pair);
                                                currentIndex++;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        currentIndex++;
                                    }
                                }
                                if (currentLoop.Count > 0)
                                {
                                    loops.Add(currentLoop);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var child in msgDef.GetChildren())
            {
                if (child.Value is FieldDef fieldDef && fieldDef.IsRequired)
                {
                    if (!fields.Exists(f => f.Tag == fieldDef.Tag))
                    {
                        errors.Add($"Missing required field: Tag {fieldDef.Tag} ({fieldDef.Name})");
                        return new ParseResult(errors, null);
                    }
                }
            }

            string unparsedText = unparsedFields.Count > 0 ? string.Join(specs.DelimiterChar.ToString(), unparsedFields) : string.Empty;

            var summary = new MessageSummary
            {
                MsgType = msgType ?? string.Empty,
                MsgName = msgDef.Name ?? $"MsgType_{msgType}",
                TotalFields = fields.Count + loops.Sum(l => l.Count),
                Header = unparsedText,
                CheckSumPassed = checkSumPassed,
                UnparsedText = unparsedText,
                TotalMessageLength = messageLength,
                LoopCount = loops.Count
            };
            summary.MessageFields.AddRange(fields);
            if (loops.Count > 0) summary.Loops.AddRange(loops);

            return new ParseResult(errors, summary);
        }

        private bool VerifyCheckSum(string message, string[] fieldPairs)
        {
            int trailerIdx = Array.FindIndex(fieldPairs, p => p.StartsWith("10="));
            if (trailerIdx < 0) return true;
            string body = string.Join(specs.DelimiterChar.ToString(), fieldPairs, 0, trailerIdx);
            int calculated = CalculateCheckSum(body);
            string checkSumValue = fieldPairs[trailerIdx].Substring(3);
            return int.TryParse(checkSumValue, out int checkSum) && calculated == checkSum;
        }

        private int CalculateCheckSum(string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            int cks = 0;
            for (int i = 0; i < buffer.Length; i++) cks += buffer[i];
            return cks % 256;
        }

        private MessageField ParseField(string tag, string value, FieldDef fieldDef, string? msgType)
        {
            string? dateFormat = null;
            if (fieldDef.Type == FieldType.Date)
            {
                if (!string.IsNullOrEmpty(msgType) && dateFormats.TryGetValue($"{msgType}:{tag}", out string? format))
                {
                    dateFormat = format;
                }
                else if (!string.IsNullOrEmpty(fieldDef.DateFormat))
                {
                    dateFormat = fieldDef.DateFormat;
                }
            }
            object? parsedValue = ParseFieldValue(value, fieldDef.Type, dateFormat, tag);
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

        private object? ParseFieldValue(string value, FieldType type, string? dateFormat, string tag)
        {
            if (string.IsNullOrEmpty(value)) return null;
            switch (type)
            {
                case FieldType.Int: return int.TryParse(value, out int i) ? (object)i : null;
                case FieldType.Double: return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? (object)d : null;
                case FieldType.Date:
                    try
                    {
                        if (!string.IsNullOrEmpty(dateFormat))
                        {
                            if (dateFormat == "HHmmssSSS" || dateFormat == "HHmmss")
                            {
                                DateTime baseDate = DateTime.UtcNow.AddHours(7).Date;
                                DateTime time = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                                DateTime combined = baseDate.Add(time.TimeOfDay);
                                return combined;
                            }
                            else
                            {
                                DateTime dt = DateTime.ParseExact(value, dateFormat, CultureInfo.InvariantCulture);
                                dt = dt.AddHours(7);
                                return dt;
                            }
                        }
                        else if (DateTime.TryParse(value, out DateTime dtDefault))
                        {
                            dtDefault = dtDefault.AddHours(7);
                            return dtDefault;
                        }
                    }
                    catch (FormatException)
                    {
                        return null;
                    }
                    return null;
                default: return value;
            }
        }

        public string? GetMsgType(string message)
        {
            string[] parts = message.Split(specs.DelimiterChar);
            foreach (var part in parts)
            {
                if (part.StartsWith("35=")) return part.Substring(3);
            }
            return null;
        }
    }
}