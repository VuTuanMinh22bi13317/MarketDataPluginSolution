using System;
using System.Collections.Generic;

namespace MarketDataPlugin
{
    // Update ParseResult to include CalculatedChecksum
    public class ParseResult
    {
        public List<string> Errors { get; }
        public List<MessageField> Fields { get; }
        public List<Loop> Loops { get; }
        public string UnparsedText { get; }
        public bool CheckSumPassed { get; }
        public int CalculatedChecksum { get; }

        public ParseResult(List<string> errors, List<MessageField> fields, List<Loop> loops, string unparsedText, bool checkSumPassed, int calculatedChecksum = 0)
        {
            Errors = errors ?? new List<string>();
            Fields = fields ?? new List<MessageField>();
            Loops = loops ?? new List<Loop>();
            UnparsedText = unparsedText ?? string.Empty;
            CheckSumPassed = checkSumPassed;
            CalculatedChecksum = calculatedChecksum;
        }
    }
}