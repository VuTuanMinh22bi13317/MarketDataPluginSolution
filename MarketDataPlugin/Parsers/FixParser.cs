using MarketDataPlugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;
        private const char SOH = '\u0001';

        public FixParser(string specsFile, string majorVersion, string minorVersion)
        {
            Console.WriteLine($"Debug: FixParser initializing with specsFile: {specsFile}, majorVersion: {majorVersion}, minorVersion: {minorVersion}");
            specs = new Specs(majorVersion, minorVersion);
            specs.Load(specsFile);
            specs.DisplayHeader();
            Console.WriteLine("\nMessage Definitions:");
            foreach (var msgDef in specs.GetAllMessages())
            {
                Console.WriteLine($"  Message Type: {msgDef.MsgType}, Name: {msgDef.Name}");
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is FieldDef field)
                    {
                        Console.WriteLine($"    Field: Tag={field.Tag}, Name={field.Name}, Type={field.Type}, DataField={field.DataField ?? "N/A"}");
                    }
                    else if (child.Value is LoopDef loop)
                    {
                        Console.WriteLine($"    Loop: Counter={loop.Counter}");
                        foreach (var loopChild in loop.GetChildren())
                        {
                            if (loopChild.Value is FieldDef loopField)
                            {
                                Console.WriteLine($"      Loop Field: Tag={loopField.Tag}, Name={loopField.Name}, Type={loopField.Type}, DataField={loopField.DataField ?? "N/A"}");
                            }
                        }
                    }
                }
            }
        }

        public void ParseFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Error: Folder '{folderPath}' does not exist.");
                return;
            }

            var files = Directory.GetFiles(folderPath, "*.raw", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine($"Error: No .raw files found in folder '{folderPath}'.");
                return;
            }

            foreach (var file in files)
            {
                Console.WriteLine($"\nProcessing file: {Path.GetFileName(file)}");
                int messageCount = 0;

                try
                {
                    string fileContent = File.ReadAllText(file)! ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        Console.WriteLine($"Warning: File {Path.GetFileName(file)} is empty.");
                        continue;
                    }

                    string[] messages = fileContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var message in messages)
                    {
                        if (string.IsNullOrWhiteSpace(message)) continue;

                        messageCount++;
                        Console.WriteLine($"\nMessage {messageCount}:");
                        var result = Parse(message.Trim()!);
                        if (!result.Success)
                        {
                            Console.WriteLine($"  Skipped - Errors: {string.Join(", ", result.Errors ?? new List<string>())}");
                        }
                    }

                    if (messageCount == 0)
                    {
                        Console.WriteLine($"Warning: No valid messages found in file {Path.GetFileName(file)}.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        public ParseResult Parse(string fixMessage)
        {
            var result = new ParseResult();
            if (string.IsNullOrEmpty(fixMessage))
            {
                result.AddError("Error: FIX message is empty or null.");
                return result;
            }

            var fieldsList = new List<KeyValuePair<string, string>>();
            string[] pairs = fixMessage.Split(SOH, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    fieldsList.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
                }
            }

            if (!fieldsList.Any(f => f.Key == "8") || fieldsList.FirstOrDefault(f => f.Key == "8").Value != specs.BeginString)
            {
                result.AddError($"Error: Invalid or missing BeginString. Expected {specs.BeginString}, but got {(fieldsList.Any(f => f.Key == "8") ? fieldsList.First(f => f.Key == "8").Value : "missing")}.");
                return result;
            }

            if (!fieldsList.Any(f => f.Key == "35"))
            {
                result.AddError("Error: MsgType (Tag 35) not found in FIX message.");
                return result;
            }
            string msgType = fieldsList.First(f => f.Key == "35").Value;

            if (!specs.TryGetMessage(msgType, out MsgDef? msgDef))
            {
                result.AddError($"Error: MsgType '{msgType}' not found in Specs.");
                return result;
            }

            LoopDef? loopDef = null;
            if (msgDef != null)
            {
                foreach (var child in msgDef.GetChildren())
                {
                    if (child.Value is LoopDef loop && loop.Counter == "NO_MD_ENTRIES")
                    {
                        loopDef = loop;
                        break;
                    }
                }
            }

            var messageFields = new List<FieldOutput>();
            var loopFields = new List<List<FieldOutput>>();
            List<FieldOutput> currentLoop = null;
            bool inLoop = false;

            foreach (var field in fieldsList)
            {
                if (field.Key == "10")
                {
                    if (inLoop && currentLoop != null && currentLoop.Count > 0)
                    {
                        loopFields.Add(currentLoop);
                        inLoop = false;
                        currentLoop = null;
                    }
                    var checkSumField = new FieldOutput
                    {
                        Tag = "10",
                        Value = field.Value,
                        Name = "CheckSum",
                        Type = "String",
                        DataField = "N/A",
                        IsLog = false
                    };
                    messageFields.Add(checkSumField);
                    continue;
                }

                if (field.Key == "83")
                {
                    if (inLoop && currentLoop != null && currentLoop.Count > 0)
                    {
                        loopFields.Add(currentLoop);
                    }
                    inLoop = true;
                    currentLoop = new List<FieldOutput>();
                    continue;
                }

                var fieldOutput = new FieldOutput
                {
                    Tag = field.Key,
                    Value = field.Value
                };

                if (specs.HeaderFields.TryGetValue(field.Key, out FieldDef? headerField))
                {
                    fieldOutput.Name = headerField.Name;
                    fieldOutput.Type = headerField.Type.ToString();
                    fieldOutput.DataField = string.IsNullOrEmpty(headerField.DataField) ? InferDataFieldFromName(headerField.Name) : headerField.DataField;
                    fieldOutput.IsLog = headerField.IsLog;
                }
                else if (inLoop && loopDef != null)
                {
                    foreach (var child in loopDef.GetChildren())
                    {
                        if (child.Value is FieldDef loopField && loopField.Tag == field.Key)
                        {
                            fieldOutput.Name = loopField.Name;
                            fieldOutput.Type = (loopField.Type.ToString() == "Unknown" || string.IsNullOrEmpty(loopField.Type.ToString()))
                                ? InferTypeFromName(loopField.Name)
                                : loopField.Type.ToString();
                            fieldOutput.DataField = string.IsNullOrEmpty(loopField.DataField) ? InferDataFieldFromName(loopField.Name) : loopField.DataField;
                            fieldOutput.IsLog = null;
                            break;
                        }
                    }
                }
                else if (msgDef != null)
                {
                    foreach (var child in msgDef.GetChildren())
                    {
                        if (child.Value is FieldDef msgField && msgField.Tag == field.Key)
                        {
                            fieldOutput.Name = msgField.Name;
                            fieldOutput.Type = msgField.Type.ToString();
                            fieldOutput.DataField = string.IsNullOrEmpty(msgField.DataField) ? InferDataFieldFromName(msgField.Name) : msgField.DataField;
                            fieldOutput.IsLog = null;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(fieldOutput.Name))
                {
                    fieldOutput.Name = "Unknown";
                    fieldOutput.Type = "Unknown";
                    fieldOutput.DataField = "N/A";
                    fieldOutput.IsLog = null;
                }

                if (inLoop && currentLoop != null)
                {
                    currentLoop.Add(fieldOutput);
                }
                else
                {
                    messageFields.Add(fieldOutput);
                }
            }

            if (inLoop && currentLoop != null && currentLoop.Count > 0)
            {
                loopFields.Add(currentLoop);
            }

            if (!fieldsList.Any(f => f.Key == "10"))
            {
                result.AddError("Error: CheckSum (Tag 10) missing.");
                return result;
            }

            result.MessageSummary = new MessageSummary
            {
                MsgType = msgType,
                MsgName = msgDef?.Name,
                MessageFields = messageFields,
                Loops = loopFields.Count > 0 ? loopFields : null
            };

            if (result.MessageSummary != null)
            {
                Console.WriteLine($"  MsgType: {result.MessageSummary.MsgType}, MsgName: {result.MessageSummary.MsgName}");
                Console.WriteLine("  Fields:");
                foreach (var field in result.MessageSummary.MessageFields ?? new List<FieldOutput>())
                {
                    string baseOutput = $"    Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, Type: {field.Type ?? "Unknown"}, DataField: {field.DataField ?? "N/A"}";
                    if (specs.HeaderFields.ContainsKey(field.Tag ?? string.Empty) || field.Tag == "10")
                    {
                        baseOutput += $", IsLog: {(field.IsLog.HasValue ? field.IsLog.Value.ToString() : "False")}";
                    }
                    Console.WriteLine(baseOutput);
                }

                if (result.MessageSummary.Loops != null)
                {
                    Console.WriteLine("  Loops:");
                    for (int i = 0; i < result.MessageSummary.Loops.Count; i++)
                    {
                        Console.WriteLine($"    Loop {i + 1}:");
                        foreach (var field in result.MessageSummary.Loops[i] ?? new List<FieldOutput>())
                        {
                            string baseOutput = $"      Tag: {field.Tag}, Name: {field.Name}, Value: {field.Value}, Type: {field.Type ?? "Unknown"}, DataField: {field.DataField ?? "N/A"}";
                            if (specs.HeaderFields.ContainsKey(field.Tag ?? string.Empty) || field.Tag == "10")
                            {
                                baseOutput += $", IsLog: {(field.IsLog.HasValue ? field.IsLog.Value.ToString() : "False")}";
                            }
                            Console.WriteLine(baseOutput);
                        }
                    }
                }
            }

            return result;
        }

        private string InferTypeFromName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unknown";

            return name switch
            {
                "MDUpdateAction" => "Int",
                "MDEntryType" => "String",
                "RptSeq" => "Int",
                "MDEntryPositionNo" => "Int",
                "MDEntryPx" => "Double",
                "MDEntrySize" => "Int",
                "NumberOfOrders" => "Int",
                "MDEntryYield" => "Double",
                "MDEntryMMSize" => "Int",
                "BeginString" => "String",
                "BodyLength" => "Int",
                "MsgType" => "String",
                "SenderCompId" => "String",
                "TargetCompId" => "String",
                "MsgSeqNum" => "Int",
                "SendingTime" => "Date",
                "MarketId" => "String",
                "BoardId" => "String",
                "TradingSessionId" => "String",
                "Symbol" => "String",
                "TradeDate" => "Date",
                "TransactTime" => "Date",
                "TotalVolumeTraded" => "Int",
                "GrossTradeAmt" => "Double",
                "SellTotQuoteQty" => "Int",
                "BuyTotQuoteQty" => "Int",
                "SellValidQuoteCnt" => "Int",
                "BuyValidQuoteCnt" => "Int",
                "NoMDEntries" => "Int",
                _ => "Unknown"
            };
        }

        private string InferDataFieldFromName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return "N/A";

            return name switch
            {
                "MDUpdateAction" => "MD_UPDATE_ACTION",
                "MDEntryType" => "MD_ENTRY_TYPE",
                "RptSeq" => "RPT_SEQ",
                "MDEntryPositionNo" => "MD_ENTRY_POSITION_NO",
                "MDEntryPx" => "MD_ENTRY_PX",
                "MDEntrySize" => "MD_ENTRY_SIZE",
                "NumberOfOrders" => "NUMBER_OF_ORDERS",
                "MDEntryYield" => "MD_ENTRY_YIELD",
                "MDEntryMMSize" => "MD_ENTRY_MM_SIZE",
                "BeginString" => "BEGIN_STRING",
                "BodyLength" => "BODY_LENGTH",
                "MsgType" => "MSG_TYPE",
                "SenderCompId" => "SENDER_COMP_ID",
                "TargetCompId" => "TARGET_COMP_ID",
                "MsgSeqNum" => "MSG_SEQ_NUM",
                "SendingTime" => "SENDING_TIME",
                "MarketId" => "MARKET_ID",
                "BoardId" => "BOARD_ID",
                "TradingSessionId" => "TRADING_SESSION_ID",
                "Symbol" => "Symbol",
                "TradeDate" => "TRADE_DATE",
                "TransactTime" => "TRANSACT_TIME",
                "TotalVolumeTraded" => "TOTAL_VOLUME_TRADED",
                "GrossTradeAmt" => "GROSS_TRADE_AMT",
                "SellTotQuoteQty" => "SELL_TOT_QUOTE_QTY",
                "BuyTotQuoteQty" => "BUY_TOT_QUOTE_QTY",
                "SellValidQuoteCnt" => "SELL_VALID_QUOTE_CNT",
                "BuyValidQuoteCnt" => "BUY_VALID_QUOTE_CNT",
                "NoMDEntries" => "NO_MD_ENTRIES",
                _ => "N/A"
            };
        }
    }

    public class ParseResult
    {
        public List<string> Errors { get; } = new List<string>();
        public MessageSummary? MessageSummary { get; set; }

        public bool Success => !HasCriticalErrors;
        public bool HasErrors => Errors.Any();
        public bool HasCriticalErrors => Errors.Any(e => e.StartsWith("Error:"));

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }

    public class MessageSummary
    {
        public string? MsgType { get; set; }
        public string? MsgName { get; set; }
        public List<FieldOutput>? MessageFields { get; set; }
        public List<List<FieldOutput>>? Loops { get; set; }
    }

    public class FieldOutput
    {
        public string? Tag { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Type { get; set; }
        public string? DataField { get; set; }
        public bool? IsLog { get; set; }
    }
}