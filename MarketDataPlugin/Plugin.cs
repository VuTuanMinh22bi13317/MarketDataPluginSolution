using MarketDataPlugin;
using System;

namespace MarketDataPlugin
{
    public class Plugin
    {
        private readonly string _configPath;

        public Plugin(string configPath)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            Specs.LoadConfig(_configPath); // Tải cấu hình khi khởi tạo Plugin
        }

        public void ProcessFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                throw new ArgumentException("Folder path cannot be null or empty.", nameof(folderPath));

            Console.WriteLine("Processing folder: " + folderPath);
            Specs.DisplayConfig();
            foreach (var result in Specs.ParseFolder(folderPath))
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