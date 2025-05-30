using System;
using System.Collections.Generic;

namespace MarketDataPlugin.DEF
{
    public class MsgDef
    {
        private readonly Dictionary<string, object> children = new Dictionary<string, object>();

        public string Name { get; }
        public string MsgType { get; }
        public DataDirection Direction { get; }

        public MsgDef(string name, string msgType, DataDirection direction)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MsgType = msgType ?? throw new ArgumentNullException(nameof(msgType));
            Direction = direction;
        }

        public void AddChild(string tag, object child)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            children[tag] = child;
        }

        public IReadOnlyDictionary<string, object> GetChildren()
        {
            return children.AsReadOnly();
        }
    }

    public static class MessageSplitter
    {
        public static List<string> SplitMessages(Specs specs, string rawData, string expectedBeginString)
        {
            var messages = new List<string>();
            int index = 0;

            Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");
            Console.WriteLine($"Debug: Expected BeginString: 8={expectedBeginString}{specs.DelimiterChar}");
            while (index < rawData.Length)
            {
                int beginIndex = rawData.IndexOf("8=", index);
                if (beginIndex == -1)
                {
                    Console.WriteLine($"Debug: No more messages found after index {index}");
                    break;
                }

                string segment = rawData.Substring(beginIndex).TrimStart(); // Trim leading whitespace/newlines
                Console.WriteLine($"Debug: Checking segment at index {beginIndex}, starts with: {segment.Substring(0, Math.Min(15, segment.Length))}...");
                if (!segment.StartsWith($"8={expectedBeginString}{specs.DelimiterChar}"))
                {
                    Console.WriteLine($"Debug: Segment at index {beginIndex} does not start with 8={expectedBeginString}{specs.DelimiterChar}, skipping.");
                    index = beginIndex + 2;
                    continue;
                }

                int bodyLength = GetBodyLength(specs, rawData, beginIndex);
                Console.WriteLine($"Debug: Found BodyLength at index {beginIndex}: {bodyLength}");
                if (bodyLength < 0)
                {
                    Console.WriteLine($"Debug: Invalid or missing BodyLength at index {beginIndex}, skipping.");
                    index = beginIndex + 2;
                    continue;
                }

                int expectedEnd = beginIndex + bodyLength + 6;
                Console.WriteLine($"Debug: Expected end at {expectedEnd}, checking for 10=");
                if (expectedEnd > rawData.Length || !rawData.Substring(expectedEnd - 6, 6).StartsWith($"10="))
                {
                    Console.WriteLine($"Debug: No valid tag 10 at expected end {expectedEnd}, skipping.");
                    index = beginIndex + 2;
                    continue;
                }

                string message = rawData.Substring(beginIndex, expectedEnd - beginIndex);
                messages.Add(message);
                Console.WriteLine($"Debug: Added message of length {message.Length}");
                index = expectedEnd;
            }

            Console.WriteLine($"Debug: Found {messages.Count} messages");
            return messages;
        }

        private static int GetBodyLength(Specs specs, string rawData, int startIndex)
        {
            int lengthStart = rawData.IndexOf($"9=", startIndex);
            if (lengthStart < 0)
                return -1;

            lengthStart += 2;
            int lengthEnd = rawData.IndexOf(specs.DelimiterChar, lengthStart);
            if (lengthEnd < 0)
                return -1;

            string lengthStr = rawData.Substring(lengthStart, lengthEnd - lengthStart);
            return int.TryParse(lengthStr, out int length) ? length : -1;
        }
    }
}