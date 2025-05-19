using MarketDataPlugin;
using Xunit;

namespace MarketDataPlugin.Tests
{
    public class SpecsTest
    {
        [Fact]
        public void TestFieldDefCreation()
        {
            var fieldDef = new FieldDef("35", "MarketId", FieldType.String, false); // Updated to string
            Assert.Equal("MarketId", fieldDef.Name);
        }
    }
}