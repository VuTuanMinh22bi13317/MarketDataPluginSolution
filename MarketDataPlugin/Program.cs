using MarketDataPlugin;
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
            Console.WriteLine("=== Specs App ===");
            Console.WriteLine($"Current Date and Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC+07:00)");
            Console.WriteLine();

            try
            {
                // Set paths for the config file and folder with .raw files
                string configFile = @"C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
                string folderPath = @"C:/Users/Admin/Downloads/20250416 (1)/20250416";

                // Check if the folder exists
                if (!Directory.Exists(folderPath))
                {
                    Console.WriteLine($"Error: Directory not found - {folderPath}");
                    return;
                }

                // Load configuration
                Specs.LoadConfig(configFile);

                // Show parser config for debugging
                Console.WriteLine("\nParser Configuration:");
                Specs.DisplayConfig();

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
                    var results = Specs.ParseFolder(folderPath);
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
                        // Show summary based on parsed fields and loops (approximation since Summary is not available)
                        else if (result.Fields != null || result.Loops != null)
                        {
                            int totalFields = (result.Fields?.Count ?? 0) + (result.Loops?.Sum(l => l.ParsedFields.Count) ?? 0);
                            bool checkSumPassed = !result.Errors.Any(e => e.Contains("Checksum failed"));
                            Console.WriteLine($"Total Fields: {totalFields}, CheckSumPassed: {checkSumPassed}");
                            writer.WriteLine($"Total Fields: {totalFields}, CheckSumPassed: {checkSumPassed}");
                            if (!string.IsNullOrEmpty(result.UnparsedText))
                            {
                                Console.WriteLine($"Warning: Unparsed fields detected (possible specs update needed): {result.UnparsedText}");
                                writer.WriteLine($"Warning: Unparsed fields detected (possible specs update needed): {result.UnparsedText}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Parsing successful with no fields or loops.");
                            writer.WriteLine("Parsing successful with no fields or loops.");
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