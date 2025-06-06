using MarketDataPlugin;
using System;
using System.Collections.Generic;

namespace MarketDataPlugin
{
    public class Plugin
    {
        private readonly FixMessageParser parser;

        public Plugin(string configPath)
        {
            parser = new FixMessageParser(configPath);
        }

        public void ProcessFolder(string folderPath)
        {
            Console.WriteLine("Processing folder: " + folderPath);
            parser.DisplayConfig();
            foreach (var result in parser.ParseFolder(folderPath))
            {
                Console.WriteLine($"Processed file: {result.fileName}");
                if (result.result.Errors.Count > 0)
                {
                    Console.WriteLine("Errors encountered:");
                    foreach (var error in result.result.Errors)
                    {
                        Console.WriteLine(error);
                    }
                }
            }
        }
    }
}