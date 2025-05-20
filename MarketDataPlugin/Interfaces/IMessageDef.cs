namespace MarketDataPlugin.Interfaces
{
    public interface IMessageDef
    {
        string Name { get; }
        string MsgType { get; }
        DataDirection Direction { get; }
        IReadOnlyList<object> Children { get; }
        void AddChild(object child);
    }
}