namespace MarketDataPlugin.Tests
{
    [TestFixture]
    public class SpecsTests
    {
        [Test]
        public void Specs_ConstructorSetsDefaultValues()
        {
            var specs = new Specs("4", "4");
            Assert.That(specs.BeginString, Is.EqualTo("FIX.4.4")); // Modern syntax
            Assert.That(specs.TimeZone, Is.EqualTo(7));
        }

        [Test]
        public void FieldDef_ConstructorSetsProperties()
        {
            var fieldDef = new FieldDef(55, "Symbol", FieldType.String, true);
            Assert.That(fieldDef.Tag, Is.EqualTo(55));
            Assert.That(fieldDef.Name, Is.EqualTo("Symbol"));
            Assert.That(fieldDef.Type, Is.EqualTo(FieldType.String));
            Assert.That(fieldDef.Required, Is.True);
        }
    }
}