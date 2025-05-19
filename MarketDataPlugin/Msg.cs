namespace MarketDataPlugin
{
    public class Msg : FixItem
    {
        public string MsgType { get; set; }
        public List<FixItem> Children { get; set; }

        public Msg(string name, string msgType)
            : base(0, name ?? throw new ArgumentNullException(nameof(name))) // Tag=0 since Msg doesn't have a tag
        {
            MsgType = msgType ?? throw new ArgumentNullException(nameof(msgType));
            Children = new List<FixItem>();
        }

        public void AddChild(FixItem child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            Children.Add(child);
        }

        public override void Parse(string data)
        {
            var tvps = data.Split('\x01', StringSplitOptions.RemoveEmptyEntries)
                .Select(tvpStr => new TVP(tvpStr))
                .Where(tvp => tvp.HasTag && tvp.HasValue)
                .ToArray();

            foreach (var tvp in tvps)
            {
                foreach (var child in Children)
                {
                    if (child is Field field && field.Tag == tvp.Tag) // No conversion needed
                    {
                        field.FromString(tvp.Value);
                    }
                }
            }
        }
    }
}