using System;
using System.Collections.Generic;

namespace MarketDataPlugin.DEF
{
    // Defines a FIX message type with its fields or loops
    public class MsgDef
    {
        private readonly Dictionary<string, object> children = new Dictionary<string, object>(); // Stores child fields or loops

        public string Name { get; } // Message name
        public string MsgType { get; } // Message type (e.g., MV)
        public DataDirection Direction { get; } // Direction of the message (In/Out)

        // Creates a new MsgDef with name, type, and direction, checks for null values
        public MsgDef(string name, string msgType, DataDirection direction)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MsgType = msgType ?? throw new ArgumentNullException(nameof(msgType));
            Direction = direction;
        }

        // Adds a child field or loop to the message, checks for null tag
        public void AddChild(string tag, object child)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            children[tag] = child;
        }

        // Returns a read-only view of all child fields and loops
        public IReadOnlyDictionary<string, object> GetChildren()
        {
            return children.AsReadOnly();
        }
    }

    // Splits raw FIX data into individual messages
    public static class MessageSplitter
    {
        // Splits raw data into messages based on BeginString and BodyLength, returns total message length
        public static List<(string Message, int TotalLength)> SplitMessages(char delimiterChar, string rawData, string expectedBeginString)
        {
            var messages = new List<(string, int)>(); // Stores extracted messages and their total length
            int index = 0; // Tracks position in raw data

            Console.WriteLine($"Debug: Processing raw data of length {rawData.Length}");
            Console.WriteLine($"Debug: Expected BeginString: 8={expectedBeginString}{delimiterChar}");
            while (index < rawData.Length)
            {
                int beginIndex = rawData.IndexOf($"8={expectedBeginString}{delimiterChar}", index); // Find exact BeginString
                if (beginIndex == -1)
                {
                    Console.WriteLine($"Debug: No more messages found after index {index}");
                    break;
                }

                string segment = rawData.Substring(beginIndex); // Get segment starting at exact BeginString
                Console.WriteLine($"Debug: Processing segment at index {beginIndex}, starts with: {segment.Substring(0, Math.Min(15, segment.Length))}...");
                int bodyLength = GetBodyLength(delimiterChar, rawData, beginIndex); // Get the body length (Tag 9)
                Console.WriteLine($"Debug: Found BodyLength at index {beginIndex}: {bodyLength}");
                if (bodyLength < 0)
                {
                    Console.WriteLine($"Debug: Invalid or missing BodyLength at index {beginIndex}, skipping.");
                    index = beginIndex + $"8={expectedBeginString}{delimiterChar}".Length;
                    continue;
                }

                // Find the end of tag 9 to determine the start of the body
                int bodyStart = rawData.IndexOf(delimiterChar, beginIndex + $"8={expectedBeginString}{delimiterChar}".Length) + 1; // Vị trí sau tag 9
                if (bodyStart < 0 || bodyStart >= rawData.Length)
                {
                    Console.WriteLine($"Debug: Invalid body start at index {beginIndex}, skipping.");
                    index = beginIndex + $"8={expectedBeginString}{delimiterChar}".Length;
                    continue;
                }

                // Find the start of tag 10 (just before the checksum value)
                int checksumStart = rawData.IndexOf($"{delimiterChar}10=", bodyStart);
                if (checksumStart < 0 || checksumStart < bodyStart + bodyLength)
                {
                    Console.WriteLine($"Debug: No valid tag 10 found after body length {bodyLength} at index {beginIndex}, skipping.");
                    index = beginIndex + $"8={expectedBeginString}{delimiterChar}".Length;
                    continue;
                }

                // Calculate actual body length (from after tag 9 to before tag 10)
                int actualBodyLength = checksumStart - bodyStart;
                if (actualBodyLength != bodyLength)
                {
                    Console.WriteLine($"Debug: BodyLength mismatch: Expected {bodyLength}, actual {actualBodyLength} at index {beginIndex}, skipping.");
                    index = beginIndex + $"8={expectedBeginString}{delimiterChar}".Length;
                    continue;
                }

                int checksumEnd = rawData.IndexOf(delimiterChar, checksumStart + 4);
                if (checksumEnd < 0) checksumEnd = rawData.Length;
                int expectedEnd = checksumEnd + 1; // Bao gồm delimiter cuối cùng
                int totalLength = expectedEnd - beginIndex; // Độ dài toàn bộ message

                string message = rawData.Substring(beginIndex, expectedEnd - beginIndex); // Extract the message
                messages.Add((message, totalLength));
                Console.WriteLine($"Debug: Added message of length {totalLength}");
                index = expectedEnd;
            }

            Console.WriteLine($"Debug: Found {messages.Count} messages");
            return messages;
        }

        // Gets the body length (Tag 9) from the raw data
        private static int GetBodyLength(char delimiterChar, string rawData, int startIndex)
        {
            int lengthStart = rawData.IndexOf($"9=", startIndex); // Find Tag 9
            if (lengthStart < 0)
                return -1;

            lengthStart += 2;
            int lengthEnd = rawData.IndexOf(delimiterChar, lengthStart); // Find end of Tag 9 value
            if (lengthEnd < 0)
                return -1;

            string lengthStr = rawData.Substring(lengthStart, lengthEnd - lengthStart); // Extract length value
            return int.TryParse(lengthStr, out int length) ? length : -1;
        }
    }
}