using System.Runtime.CompilerServices;

namespace MarketDataPlugin
{
    public abstract class MsgChildDef : FixItem
    {
        public bool Required { get; set; }

        protected MsgChildDef(int tag, string name, bool required)
            : base(tag, name)
        {
            Required = required;
            
        }

        public override void Parse(string data) // Added override keyword
        {
            // Abstract method, to be implemented by derived classes
            throw new NotImplementedException("Parse must be implemented by derived classes.");
        }
    }
}