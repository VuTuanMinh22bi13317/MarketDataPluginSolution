namespace MarketDataPlugin
{
    public enum FieldType
    {
        String,
        Int,
        DateTime,  // Handles both date and datetime formats
        Double,    // Replaces Float for floating-point numbers
        Boolean
    }
}