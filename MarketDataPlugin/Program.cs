using MarketDataPlugin.Parsers;
using System;

namespace MarketDataPlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            string specsFile = "C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
            string folderPath = "C:/Users/Admin/Downloads/20250416 (1)/20250416";
            var parser = new FixParser(specsFile, "4", "4");
            parser.ParseFolder(folderPath);
        }
    }
}