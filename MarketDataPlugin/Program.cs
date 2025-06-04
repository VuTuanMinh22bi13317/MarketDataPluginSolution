using MarketDataPlugin;
using MarketDataPlugin.Parsers;
using System;
using System.IO;

namespace FixMessageParserApp
{
    class Program
    {
        // Main entry point of the app, processes FIX messages and saves results
        static void Main(string[] args)
        {
            // Show app header with current date and time
            Console.WriteLine("=== FIX Message Parser App ===");
            Console.WriteLine($"Current Date and Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC+07:00)");
            Console.WriteLine();

            try
            {
                // Set paths for the config file and folder with .raw files
                string specsFile = @"C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
                string folderPath = @"C:/Users/Admin/Downloads/20250416 (1)/20250416";

                // Check if the folder exists
                if (!Directory.Exists(folderPath))
                {
                    Console.WriteLine($"Error: Directory not found - {folderPath}");
                    return;
                }

                // Load specs from the config file
                var specs = Specs.Load(specsFile, "4", "4");

                // Create a parser using the loaded specs
                var parser = new FixParser(specs, specsFile);

                // Show parser config for debugging
                Console.WriteLine("\nParser Configuration:");
                parser.DisplayConfig();

                // Create output directory and file to save results
                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                string outputFile = Path.Combine(outputDir, $"parsed_output_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                // Process files and save results to console and file
                using (var writer = new StreamWriter(outputFile))
                {
                    writer.WriteLine("=== FIX Message Parser Output ===");
                    writer.WriteLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC+07:00)");
                    writer.WriteLine();

                    // Loop through each file and parse it
                    var results = parser.ParseFolder(folderPath);
                    foreach (var (fileName, result) in results)
                    {
                        string displayFileName = Path.GetFileName(fileName);
                        Console.WriteLine($"\nResults for file: {displayFileName}");
                        writer.WriteLine($"File: {displayFileName}");

                        // Show errors if any occurred
                        if (result.Errors.Count > 0)
                        {
                            Console.WriteLine("Errors encountered:");
                            writer.WriteLine("Errors:");
                            for (int i = 0; i < result.Errors.Count; i++)
                            {
                                Console.WriteLine($"  - {result.Errors[i]}");
                                writer.WriteLine($"  - {result.Errors[i]}");
                            }
                        }
                        // Show summary if parsing worked
                        else if (result.Summary != null)
                        {
                            Console.WriteLine($"MsgType: {result.Summary.MsgType}, MsgName: {result.Summary.MsgName}, TotalFields: {result.Summary.TotalFields}, CheckSumPassed: {result.Summary.CheckSumPassed}");
                            writer.WriteLine($"MsgType: {result.Summary.MsgType}, MsgName: {result.Summary.MsgName}, TotalFields: {result.Summary.TotalFields}, CheckSumPassed: {result.Summary.CheckSumPassed}");
                            if (!string.IsNullOrEmpty(result.Summary.UnparsedText))
                            {
                                Console.WriteLine($"Warning: Unparsed fields detected (possible specs update needed): {result.Summary.UnparsedText}");
                                writer.WriteLine($"Warning: Unparsed fields detected (possible specs update needed): {result.Summary.UnparsedText}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Parsing successful with no summary.");
                            writer.WriteLine("Parsing successful with no summary.");
                        }
                        Console.WriteLine();
                        writer.WriteLine();
                    }
                }

                // Tell user where the output file is saved
                Console.WriteLine($"\nOutput saved to: {outputFile}");
                Console.WriteLine("Processing complete.");
            }
            catch (Exception ex)
            {
                // Show any errors and stack trace
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            // Wait for user to press a key before closing
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}