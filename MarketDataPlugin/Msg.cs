using MarketDataPlugin.DEF;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MarketDataPlugin
{
    public class Msg
    {
        private readonly Specs specs;
        private readonly MsgDef? msgDef;
        private readonly ParseResult result = new ParseResult();
        private readonly List<KeyValuePair<string, string>> fieldList;

        public Msg(Specs specs, string rawMessage)
        {
            this.specs = specs ?? throw new ArgumentNullException(nameof(specs));
            this.fieldList = ParseRawMessage(rawMessage);

            if (!fieldList.Any())
            {
                result.AddError("Error: No fields parsed from raw message.");
                return;
            }

            // Extract MsgType (Tag 35)
            var msgTypeField = fieldList.FirstOrDefault(f => f.Key == "35");
            if (msgTypeField.Key == null || string.IsNullOrEmpty(msgTypeField.Value) || !specs.Messages.TryGetValue(msgTypeField.Value, out msgDef))
            {
                result.AddError($"Error: Invalid or undefined MsgType: {msgTypeField.Value ?? "null"}.");
                return;
            }

            // Validate BodyLength (Tag 9)
            var lengthField = fieldList.FirstOrDefault(f => f.Key == "9");
            if (lengthField.Key == null || !int.TryParse(lengthField.Value, out int bodyLength))
            {
                result.AddError("Error: Invalid or missing BodyLength (Tag 9).");
                return;
            }

            int calculatedLength = CalculateMessageLength();
            if (bodyLength != calculatedLength)
            {
                result.AddError($"Error: BodyLength mismatch. Expected {bodyLength}, calculated {calculatedLength}.");
                return;
            }

            // Parse fields and loops
            ParseFieldsAndLoops();

            // Validate CheckSum (Tag 10) if required
            if (specs.InMsgChecksum)
            {
                var checksumField = fieldList.FirstOrDefault(f => f.Key == "10");
                if (checksumField.Key != null)
                {
                    int trailerIdx = rawMessage.LastIndexOf($"10{specs.DelimiterChar}");
                    if (trailerIdx >= 0)
                    {
                        string messageWithoutChecksum = rawMessage.Substring(0, trailerIdx);
                        int calculatedChecksum = CalculateCheckSum(messageWithoutChecksum);
                        if (!int.TryParse(checksumField.Value, out int providedChecksum) || calculatedChecksum != providedChecksum)
                        {
                            result.AddError($"Error: CheckSum mismatch. Expected: {providedChecksum}, Calculated: {calculatedChecksum}.");
                            return;
                        }
                        Console.WriteLine($"Debug: CheckSum - Expected: {providedChecksum}, Calculated: {calculatedChecksum}, Passed: True");
                    }
                }
                else
                {
                    result.AddError("Error: Missing CheckSum (Tag 10).");
                    return;
                }
            }

            // Initialize MessageSummary with pre-populated fields
            result.MessageSummary = new MessageSummary
            {
                MsgType = msgTypeField.Value,
                MsgName = msgDef?.Name,
                TotalFields = result.MessageFields.Count + (result.Loops?.Sum(l => l.Count) ?? 0),
                // Use the existing MessageFields and Loops from result
                // No assignment to MessageFields needed since it’s read-only and pre-populated
            };
            result.MessageSummary.MessageFields.AddRange(result.MessageFields); // Copy fields to MessageSummary
            result.MessageSummary.Loops = result.Loops; // Assign Loops (nullable, so this is fine)
        }

        private List<KeyValuePair<string, string>> ParseRawMessage(string rawMessage)
        {
            var fields = new List<KeyValuePair<string, string>>();
            int index = 0;

            while (index < rawMessage.Length)
            {
                int eqIdx = rawMessage.IndexOf('=', index);
                if (eqIdx < 0 || eqIdx >= rawMessage.Length - 1) break;

                string tag = rawMessage.Substring(index, eqIdx - index);
                int nextDelimIdx = rawMessage.IndexOf(specs.DelimiterChar, eqIdx + 1);
                if (nextDelimIdx < 0) nextDelimIdx = rawMessage.Length;

                string value = rawMessage.Substring(eqIdx + 1, nextDelimIdx - eqIdx - 1);
                fields.Add(new KeyValuePair<string, string>(tag, value));
                index = nextDelimIdx + 1;
            }

            return fields;
        }

        private int CalculateMessageLength()
        {
            int length = 0;
            bool afterBodyLength = false;
            foreach (var field in fieldList)
            {
                if (field.Key == "9")
                {
                    afterBodyLength = true;
                    continue;
                }
                if (field.Key == "10") break;
                if (afterBodyLength)
                    length += field.Key.Length + 1 + field.Value.Length + 1; // Tag + '=' + Value + Delimiter
            }
            return length;
        }

        private void ParseFieldsAndLoops()
        {
            var remainingFields = new List<KeyValuePair<string, string>>(fieldList);
            string? loopStartTag = null;

            // Find loop start tag
            if (msgDef != null)
            {
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is LoopDef loopDef)
                    {
                        loopStartTag = loopDef.Tag;
                        break;
                    }
                }
            }

            // Parse header fields
            foreach (var headerField in specs.HeaderFields)
            {
                var field = remainingFields.FirstOrDefault(f => f.Key == headerField.Value.Tag);
                if (field.Key != null)
                {
                    var parsedValue = ParseFieldValue(headerField.Value, field.Value);
                    result.MessageFields.Add(new FieldOutput
                    {
                        Tag = headerField.Value.Tag,
                        Name = headerField.Value.Name,
                        Value = parsedValue,
                        Type = headerField.Value.Type.ToString(),
                        DataField = headerField.Value.DataField,
                        IsLog = headerField.Value.IsLog
                    });
                    remainingFields.Remove(field);
                }
                else if (headerField.Value.IsRequired)
                {
                    result.AddError($"Error: Required header field {headerField.Value.Tag} ({headerField.Value.Name}) not found.");
                }
            }

            // Parse body fields and loops
            List<FieldOutput>? currentLoop = null;

            if (msgDef != null)
            {
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is FieldDef fieldDef)
                    {
                        var field = remainingFields.FirstOrDefault(f => f.Key == fieldDef.Tag);
                        if (field.Key != null && field.Key != loopStartTag)
                        {
                            var parsedValue = ParseFieldValue(fieldDef, field.Value);
                            result.MessageFields.Add(new FieldOutput
                            {
                                Tag = fieldDef.Tag,
                                Name = fieldDef.Name,
                                Value = parsedValue,
                                Type = fieldDef.Type.ToString(),
                                DataField = fieldDef.DataField,
                                IsLog = fieldDef.IsLog
                            });
                            remainingFields.Remove(field);
                        }
                        else if (fieldDef.IsRequired)
                        {
                            result.AddError($"Error: Required field {fieldDef.Tag} ({fieldDef.Name}) not found.");
                        }
                    }
                }

                // Process loops
                foreach (var field in remainingFields.ToList())
                {
                    if (field.Key == loopStartTag && msgDef.GetChildren().TryGetValue(field.Key, out var loopObj) && loopObj is LoopDef loopDef)
                    {
                        if (currentLoop != null && currentLoop.Any())
                        {
                            result.Loops.Add(currentLoop);
                        }
                        currentLoop = new List<FieldOutput>();
                        currentLoop.Add(new FieldOutput
                        {
                            Tag = loopDef.Tag,
                            Name = loopDef.Name,
                            Value = ParseFieldValue(new FieldDef(loopDef.Tag, loopDef.Name, FieldType.Int, loopDef.Counter, false, null, false), field.Value),
                            Type = FieldType.Int.ToString(),
                            DataField = loopDef.Counter,
                            IsLog = false
                        });
                        remainingFields.Remove(field);
                    }
                    else if (currentLoop != null && loopStartTag != null && msgDef.GetChildren().ContainsKey(loopStartTag))
                    {
                        if (msgDef.GetChildren()[loopStartTag] is LoopDef innerLoopDef)
                        {
                            if (innerLoopDef.GetChildren().TryGetValue(field.Key, out var fieldDef))
                            {
                                var parsedValue = ParseFieldValue(fieldDef, field.Value);
                                currentLoop.Add(new FieldOutput
                                {
                                    Tag = fieldDef.Tag,
                                    Name = fieldDef.Name,
                                    Value = parsedValue,
                                    Type = fieldDef.Type.ToString(),
                                    DataField = fieldDef.DataField,
                                    IsLog = fieldDef.IsLog
                                });
                                remainingFields.Remove(field);
                            }
                        }
                    }
                }

                if (currentLoop != null && currentLoop.Any())
                {
                    result.Loops.Add(currentLoop);
                }
            }

            if (remainingFields.Any())
            {
                result.AddError($"Warning: Found {remainingFields.Count} undefined fields: {string.Join(", ", remainingFields.Select(f => f.Key))}");
            }
        }

        private object? ParseFieldValue(FieldDef fieldDef, string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                return fieldDef.Type switch
                {
                    FieldType.Int => int.TryParse(value, out int intValue) ? (object)intValue : throw new FormatException($"Cannot parse '{value}' as Int"),
                    FieldType.Double => double.TryParse(value, out double doubleValue) ? (object)doubleValue : throw new FormatException($"Cannot parse '{value}' as Double"),
                    FieldType.Date => ParseDateField(value, fieldDef.DateFormat),
                    FieldType.String => (object)value,
                    _ => throw new ArgumentException($"Unsupported FieldType: {fieldDef.Type}")
                };
            }
            catch (Exception ex)
            {
                result.AddError($"Error parsing field {fieldDef.Tag} ({fieldDef.Name}): {ex.Message}");
                return null;
            }
        }

        private DateTime? ParseDateField(string value, string? dateFormat)
        {
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "yyyyMMdd"; // Default FIX date format

            if (DateTime.TryParseExact(value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                int offset = GetTimeZoneOffset(specs.TimeZone ?? "UTC+7");
                return date.AddHours(offset);
            }
            throw new FormatException($"Cannot parse '{value}' as Date with format {dateFormat}");
        }

        private int GetTimeZoneOffset(string timeZone)
        {
            return timeZone.ToUpper() == "UTC+7" ? 7 : 0; // Vietnam offset
        }

        private int CalculateCheckSum(string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            int cks = 0;
            foreach (byte b in buffer)
                cks += b;
            return cks % 256;
        }

        public ParseResult GetResult() => result;
    }
}