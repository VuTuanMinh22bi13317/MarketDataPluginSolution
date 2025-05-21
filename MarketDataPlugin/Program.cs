using MarketDataPlugin.Parsers;

namespace MarketDataPlugin
{
    class Program
    {
        static void Main(string[] args)
        {
            string specsFile = "C:/Users/Admin/MarketDataPluginSolution/MarketDataPlugin/Specs/MDDS-Specs.xml";

            // Tạo và chạy SpecProcessor để xem định nghĩa
            var processor = new SpecProcessor(specsFile, "4", "4");
            processor.Process();
        }
    }
}