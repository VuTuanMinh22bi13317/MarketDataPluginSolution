using MarketDataPlugin;
using MarketDataPlugin.Parsers;
using System;
using System.Linq;

namespace MarketDataPlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string specsFile = "C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
                string folderPath = "C:/Users/Admin/Downloads/20250416 (1)/20250416";

                if (!System.IO.Directory.Exists(folderPath))
                {
                    Console.WriteLine($"Error: Directory not found - {folderPath}");
                    return;
                }

                var specs = Specs.Load(specsFile, "4", "4");
                var parser = new FixParser(specs, specsFile); // Pass the configPath

                parser.DisplayConfig();

                var files = System.IO.Directory.GetFiles(folderPath, "*.raw");
                Console.WriteLine($"Found {files.Length} .raw files to process in {folderPath}");

                var results = parser.ParseFolder(folderPath);
                foreach (var (fileName, result) in results)
                {
                    Console.WriteLine($"\nResults for file: {System.IO.Path.GetFileName(fileName)}");
                    if (result.Errors.Any())
                    {
                        foreach (var error in result.Errors)
                        {
                            Console.WriteLine($"  - {error}");
                        }
                    }
                    else if (result.MessageSummary != null)
                    {
                        Console.WriteLine($"MsgType: {result.MessageSummary.MsgType}, MsgName: {result.MessageSummary.MsgName}, TotalFields: {result.MessageSummary.TotalFields}");
                    }
                    else
                    {
                        Console.WriteLine("Parsing successful with no summary.");
                    }
                }

                Console.WriteLine("Processing complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
}