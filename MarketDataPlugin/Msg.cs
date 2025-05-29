using MarketDataPlugin.DEF;
using MarketDataPlugin.Parsers;
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
        private readonly MsgDef? msgDef; // Made nullable
        private readonly ParseResult result = new ParseResult();
        private List<KeyValuePair<string, string>> fieldList;
        public bool CheckSumPassed { get; private set; } = true;

        public Msg(Specs specs, string rawMessage)
        {
            this.specs = specs ?? throw new ArgumentNullException(nameof(specs));

            fieldList = ParseRawMessage(rawMessage);
            if (fieldList.Count == 0)
                throw new ArgumentException("No fields parsed from raw message.");

            // Validate BodyLength (Tag 9)
            var lengthField = fieldList.FirstOrDefault(f => f.Key == "9");
            if (lengthField.Key == null || !int.TryParse(lengthField.Value, out int bodyLength))
            {
                result.AddError("Error: Invalid or missing BodyLength (tag 9). Discarding message.");
                return; // Discard invalid message
            }

            int calculatedLength = CalculateMessageLength(fieldList) - 6; // Exclude Tag 10
            if (bodyLength != calculatedLength)
            {
                result.AddError($"Error: BodyLength mismatch. Expected {bodyLength}, calculated {calculatedLength}. Discarding message.");
                return; // Discard invalid message
            }

            // Validate CheckSum (Tag 10) if required
            if (specs.InMsgChecksum)
            {
                var checksumField = fieldList.FirstOrDefault(f => f.Key == "10");
                if (checksumField.Key != null && int.TryParse(checksumField.Value, out int providedChecksum))
                {
                    int trailerIdx = rawMessage.LastIndexOf("10="); // Ensure we get the last occurrence
                    if (trailerIdx >= 0)
                    {
                        string messageWithoutChecksum = rawMessage.Substring(0, trailerIdx);
                        int calculatedChecksum = _CalculateCheckSum(messageWithoutChecksum);
                        CheckSumPassed = calculatedChecksum == providedChecksum;
                        Console.WriteLine($"Debug: Calculated CheckSum: {calculatedChecksum}, Provided: {providedChecksum}, Passed: {CheckSumPassed}");
                        if (!CheckSumPassed)
                        {
                            result.AddError("Error: Checksum mismatch. Discarding message.");
                            return; // Discard if checksum fails
                        }
                    }
                }
                else
                {
                    result.AddError("Error: Missing or invalid CheckSum (tag 10). Discarding message.");
                    return; // Discard if checksum is missing/invalid
                }
            }

            // Find MsgType and get MsgDef
            var msgTypeField = fieldList.FirstOrDefault(f => f.Key == "35");
            if (msgTypeField.Key == null || string.IsNullOrEmpty(msgTypeField.Value) || !specs.Messages.TryGetValue(msgTypeField.Value ?? "", out msgDef))
            {
                result.AddError($"Error: Invalid or undefined MsgType: {msgTypeField.Value ?? "null"}. Discarding message.");
                return; // Discard invalid message
            }

            // Parse header fields
            ParseHeaderFields();

            // Parse body fields and loops
            ParseBodyAndLoops();

            result.MessageSummary = new MessageSummary
            {
                MsgType = msgTypeField.Value,
                MsgName = msgDef?.Name,
                TotalFields = fieldList.Count,
                MessageFields = result.MessageFields,
                Loops = result.Loops
            };
        }

        private List<KeyValuePair<string, string>> ParseRawMessage(string rawMessage)
        {
            var fieldsList = new List<KeyValuePair<string, string>>();
            int idx = 0;

            while (idx < rawMessage.Length)
            {
                int eqIdx = rawMessage.IndexOf('=', idx);
                if (eqIdx < 0 || eqIdx >= rawMessage.Length - 1) break;

                string tag = rawMessage.Substring(idx, eqIdx - idx);
                int nextDelimIdx = rawMessage.IndexOf(Specs.DELIMITER_CHAR, eqIdx + 1);
                if (nextDelimIdx < 0) nextDelimIdx = rawMessage.Length;

                string value = rawMessage.Substring(eqIdx + 1, nextDelimIdx - eqIdx - 1);
                fieldsList.Add(new KeyValuePair<string, string>(tag, value));
                idx = nextDelimIdx + 1;
            }

            return fieldsList;
        }

        private int CalculateMessageLength(List<KeyValuePair<string, string>> fields)
        {
            int length = 0;
            foreach (var field in fields)
            {
                if (field.Key != "10") // Exclude checksum
                    length += field.Key.Length + 1 + field.Value.Length + 1; // Tag + '=' + Value + DELIMITER_CHAR
            }
            return length;
        }

        private void ParseHeaderFields()
        {
            foreach (var headerField in specs.HeaderFields)
            {
                var field = fieldList.FirstOrDefault(f => f.Key == headerField.Value.Tag);
                if (field.Key != null)
                {
                    var fieldOutput = new FieldOutput
                    {
                        Tag = headerField.Value.Tag,
                        Name = headerField.Value.Name,
                        Value = ParseFieldValue(headerField.Value, field.Value),
                        Type = headerField.Value.Type.ToString(),
                        DataField = headerField.Value.DataField,
                        IsLog = headerField.Value.IsLog
                    };
                    result.MessageFields.Add(fieldOutput);
                    fieldList.Remove(field); // Remove processed header field
                }
                else if (headerField.Value.IsRequired)
                {
                    result.AddError($"Error: Required header field {headerField.Value.Tag} ({headerField.Value.Name}) not found.");
                }
            }
        }

        private void ParseBodyAndLoops()
        {
            var remainingFields = new List<KeyValuePair<string, string>>(fieldList);
            string? loopStartTag = null;

            // Parse body fields
            foreach (var child in msgDef?.GetChildren() ?? Enumerable.Empty<KeyValuePair<string, object>>())
            {
                if (child.Value is FieldDef fieldDef)
                {
                    var field = remainingFields.FirstOrDefault(f => f.Key == fieldDef.Tag);
                    if (field.Key != null && (fieldDef.IsRequired || !string.IsNullOrEmpty(field.Value)))
                    {
                        var fieldOutput = new FieldOutput
                        {
                            Tag = fieldDef.Tag,
                            Name = fieldDef.Name,
                            Value = ParseFieldValue(fieldDef, field.Value),
                            Type = fieldDef.Type.ToString(),
                            DataField = fieldDef.DataField,
                            IsLog = fieldDef.IsLog
                        };
                        result.MessageFields.Add(fieldOutput);
                        remainingFields.Remove(field); // Remove processed body field
                    }
                    else if (fieldDef.IsRequired)
                    {
                        result.AddError($"Error: Required field {fieldDef.Tag} ({fieldDef.Name}) not found.");
                    }
                }
                else if (child.Value is LoopDef loopDef)
                {
                    loopStartTag = loopDef.Tag;
                }
            }

            // Process loops
            if (loopStartTag != null && msgDef != null)
            {
                var currentLoopFields = new List<KeyValuePair<string, string>>();
                bool inLoop = false;

                for (int i = 0; i < remainingFields.Count; i++)
                {
                    var field = remainingFields[i];
                    if (field.Key == loopStartTag)
                    {
                        if (inLoop && currentLoopFields.Any())
                        {
                            ProcessLoop(currentLoopFields);
                            remainingFields.RemoveRange(0, currentLoopFields.Count);
                            i = -1; // Restart loop
                        }
                        currentLoopFields = new List<KeyValuePair<string, string>> { field };
                        inLoop = true;
                    }
                    else if (inLoop)
                    {
                        currentLoopFields.Add(field);
                    }
                }

                if (inLoop && currentLoopFields.Any())
                {
                    ProcessLoop(currentLoopFields);
                }
            }

            // Report remaining undefined fields
            if (remainingFields.Any())
            {
                result.AddError($"Warning: Found {remainingFields.Count} undefined fields: {string.Join(", ", remainingFields.Select(f => f.Key))}");
            }
        }

        private void ProcessLoop(List<KeyValuePair<string, string>> loopFields)
        {
            var loopOutput = new List<FieldOutput>();
            if (msgDef == null) return;

            foreach (var child in msgDef.GetChildren())
            {
                if (child.Value is LoopDef loopDef)
                {
                    foreach (var loopChild in loopDef.GetChildren())
                    {
                        if (loopChild.Value is FieldDef fieldDef)
                        {
                            var field = loopFields.FirstOrDefault(f => f.Key == fieldDef.Tag);
                            if (field.Key != null && (fieldDef.IsRequired || !string.IsNullOrEmpty(field.Value)))
                            {
                                var fieldOutput = new FieldOutput
                                {
                                    Tag = fieldDef.Tag,
                                    Name = fieldDef.Name,
                                    Value = ParseFieldValue(fieldDef, field.Value),
                                    Type = fieldDef.Type.ToString(),
                                    DataField = fieldDef.DataField,
                                    IsLog = fieldDef.IsLog
                                };
                                loopOutput.Add(fieldOutput);
                            }
                            else if (fieldDef.IsRequired)
                            {
                                result.AddError($"Error: Required loop field {fieldDef.Tag} ({fieldDef.Name}) not found in loop.");
                            }
                        }
                    }
                    break;
                }
            }

            if (loopOutput.Any())
            {
                result.Loops.Add(loopOutput);
                Console.WriteLine($"Debug: Added loop with fields: {string.Join(", ", loopOutput.Select(f => $"{f.Tag}={f.Value}"))}");
            }
        }

        private object? ParseFieldValue(FieldDef fieldDef, string? value)
        {
            if (value == null) return null;

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

        private object? ParseDateField(string value, string? dateFormat)
        {
            if (string.IsNullOrEmpty(dateFormat))
                throw new ArgumentException("DateFormat is required for Date fields.");

            if (DateTime.TryParseExact(value, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date.AddHours(specs.TimeZone); // Adjust for Vietnam timezone
            }
            throw new FormatException($"Cannot parse '{value}' as Date with format {dateFormat}");
        }

        private static int _CalculateCheckSum(string str)
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