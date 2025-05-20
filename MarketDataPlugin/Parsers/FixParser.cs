namespace MarketDataPlugin
{
    public static class FixParser
    {
        public static void ParseSpecsFromXml(string xmlFilePath, Specs specs)
        {
            specs.Load(xmlFilePath); // Đổi từ LoadFromXml thành Load
        }
    }
}