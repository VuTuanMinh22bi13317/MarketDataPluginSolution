using System;
using System.Collections.Generic;
using System.IO;
using MarketDataPlugin.DEF;

namespace MarketDataPlugin
{
    public class FixMessageParser
    {
        private readonly string _beginString = "FIX.4.4";
        private readonly char _delimiterChar = '\x01';
        private readonly bool _inMsgChecksum = true;
        private readonly Dictionary<string, MsgDef> _msgDefs;

        public string BeginString => _beginString;
        public char DelimiterChar => _delimiterChar;
        public bool InMsgChecksum => _inMsgChecksum;
        public Dictionary<string, MsgDef> MsgDefs => _msgDefs;

        public FixMessageParser(string configPath)
        {
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                throw new ArgumentException($"Config file not found: {configPath}");
            _msgDefs = LoadConfig(configPath);
        }

        private Dictionary<string, MsgDef> LoadConfig(string configPath)
        {
            var msgDefs = new Dictionary<string, MsgDef>();
            // Placeholder: Thay bằng logic thực tế để tải MsgDef từ file XML hoặc cấu hình trong DEF
            msgDefs["MV"] = new MsgDef("MarketData", "MV", DataDirection.In);
            return msgDefs;
        }

        public void DisplayConfig()
        {
            Console.WriteLine("FixMessageParser Configuration:");
            Console.WriteLine($"InMsgChecksum: {_inMsgChecksum}");
            Console.WriteLine($"DelimiterChar: {(int)_delimiterChar}");
            Console.WriteLine($"BeginString: {_beginString}");
            Console.WriteLine("Message Definitions:");
            foreach (var msgDef in _msgDefs)
            {
                Console.WriteLine($"  MsgType: {msgDef.Key}, Name: {msgDef.Value.Name}, Direction: {msgDef.Value.Direction}");
            }
        }

        public IEnumerable<(string fileName, ParseResult result)> ParseFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                yield return ("", new ParseResult(new List<string> { $"Error: Directory not found - {folderPath}" }, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0));
                yield break;
            }
            string[] filePaths = Directory.GetFiles(folderPath, "*.raw");
            Console.WriteLine($"Found {filePaths.Length} .raw files to process in {folderPath}");
            for (int i = 0; i < filePaths.Length; i++)
            {
                yield return (Path.GetFileName(filePaths[i]), ParseFile(filePaths[i]));
            }
        }

        public ParseResult ParseFile(string filePath)
        {
            var errors = new List<string>();
            var logBatch = new List<(int messageNumber, string logEntry)>();

            try
            {
                string logsDir = @"C:\Users\Admin\MarketDataPluginSolution\parsed_log";
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                string logFilePath = Path.Combine(logsDir, $"parsed_{Path.GetFileName(filePath)}.log");

                var rawData = File.ReadAllText(filePath);
                Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");

                var messages = MessageSplitter.SplitMessages(DelimiterChar, rawData, BeginString);
                for (int i = 0; i < messages.Count; i++)
                {
                    var (message, _) = messages[i];
                    int messageNumber = i + 1;
                    Console.WriteLine($"Debug: Processing message {messageNumber} from {Path.GetFileName(filePath)}");

                    var messageErrors = new List<string>();
                    string? msgType = ExtractMsgType(message);
                    if (msgType != null && _msgDefs.TryGetValue(msgType, out MsgDef? msgDef))
                    {
                        var msg = new Msg(msgDef, InMsgChecksum);
                        var parseResult = msg.Parse(message, messageErrors);

                        if (parseResult == null)
                        {
                            errors.AddRange(messageErrors.Select(e => $"Message {messageNumber}: {e}"));
                            logBatch.Add((messageNumber, $"Message {messageNumber} failed: {string.Join(", ", messageErrors)}"));
                        }
                        else
                        {
                            logBatch.Add((messageNumber, $"Message {messageNumber} parsed successfully"));
                        }
                    }
                    else
                    {
                        errors.Add($"Message {messageNumber}: Unknown MsgType or invalid message");
                        logBatch.Add((messageNumber, $"Message {messageNumber} failed: Unknown MsgType"));
                    }
                }

                if (logBatch.Count > 0)
                {
                    WriteLogBatch(logFilePath, logBatch);
                    logBatch.Clear();
                }

                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse file: {Path.GetFileName(filePath)}: {ex.Message}");
                return new ParseResult(errors, new List<MessageField>(), new List<Loop>(), string.Empty, true, 0);
            }
        }

        private void WriteLogBatch(string logFilePath, List<(int messageNumber, string logEntry)> logBatch)
        {
            logBatch.Sort((a, b) => a.messageNumber.CompareTo(b.messageNumber));
            using (var logWriter = new StreamWriter(logFilePath, true))
            {
                foreach (var entry in logBatch)
                {
                    logWriter.WriteLine($"{entry.logEntry}");
                }
            }
        }

        private string? ExtractMsgType(string message)
        {
            string[] fieldPairs = message.Split(DelimiterChar); // Sửa thành DelimiterChar
            foreach (var pair in fieldPairs)
            {
                if (pair.StartsWith("35="))
                {
                    return pair.Substring(3);
                }
            }
            return null;
        }
    }
}