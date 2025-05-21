using MarketDataPlugin;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;

        public FixParser(string specsFile, string majorVersion, string minorVersion)
        {
            specs = new Specs(majorVersion, minorVersion);
            specs.Load(specsFile);
        }

        // Thêm logic parse nếu cần
        public void Parse()
        {
            // Logic parse FIX messages
            Console.WriteLine("Parsing FIX messages...");
        }
    }
}