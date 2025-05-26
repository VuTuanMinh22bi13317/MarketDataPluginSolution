using MarketDataPlugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Plugin plugin;

        public FixParser(string specsFile, string majorVersion, string minorVersion)
        {
            plugin = new Plugin(specsFile, majorVersion, minorVersion);
            plugin.DisplayHeader();
            plugin.DisplayMessages();
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
                    string fileContent = File.ReadAllText(file) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        Console.WriteLine($"Warning: File {Path.GetFileName(file)} is empty.");
                        continue;
                    }

                    foreach (var result in plugin.ProcessRawData(fileContent))
                    {
                        messageCount++;
                        Console.WriteLine($"\nMessage {messageCount}:");
                        if (result != null && result.MessageSummary != null)
                        {
                            Console.WriteLine($"  MsgType: {result.MessageSummary.MsgType}, MsgName: {result.MessageSummary.MsgName}, Fields count: {result.MessageSummary.TotalFields}");
                            if (result.Errors != null && result.Errors.Any())
                            {
                                Console.WriteLine($"  Errors: {string.Join(", ", result.Errors)}");
                            }
                        }
                        else if (result != null && result.Errors != null)
                        {
                            Console.WriteLine($"  Skipped - Errors: {string.Join(", ", result.Errors)}");
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
        public int TotalFields { get; set; }
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