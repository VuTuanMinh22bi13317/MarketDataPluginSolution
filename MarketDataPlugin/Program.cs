using System;
using MarketDataPlugin;

namespace MarketDataPlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            string xmlFilePath = args.Length > 0 ? args[0] : "C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";
            SpecProcessor.ProcessSpecs(xmlFilePath);
        }
    }
}