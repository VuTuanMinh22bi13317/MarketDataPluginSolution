namespace MarketDataPlugin.Interfaces
{
    public interface ILoopDef
    {
        string Tag { get; }
        string Name { get; }
        string Counter { get; }
        bool Required { get; }
        IReadOnlyList<object> Children { get; }
        void AddChild(object child);
    }
}