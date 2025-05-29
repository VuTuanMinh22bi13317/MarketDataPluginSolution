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
        public static List<string> SplitMessages(string rawData, string expectedBeginString)
        {
            var messages = new List<string>();
            int index = 0;

            while (index < rawData.Length)
            {
                // Kiểm tra BeginString, nếu không đúng thì bỏ toàn bộ
                string segment = rawData.Substring(index);
                if (!segment.StartsWith($"8={expectedBeginString}{Specs.DELIMITER_CHAR}"))
                    return new List<string>(); // Bỏ nếu BeginString không khớp

                // Tìm tag 9 (BodyLength)
                int bodyLength = GetBodyLength(rawData, index);
                if (bodyLength < 0)
                    return new List<string>(); // Bỏ nếu không tìm thấy tag 9

                // Tính độ dài message (BodyLength + 6 cho tag 10 và delimiter)
                int expectedEnd = index + bodyLength + 6; // 6 = "10=xxx" + delimiter
                if (expectedEnd >= rawData.Length || !rawData.Substring(expectedEnd - 6, 6).StartsWith($"10="))
                    return new List<string>(); // Bỏ nếu không có tag 10

                string message = rawData.Substring(index, expectedEnd - index);
                messages.Add(message);
                index = expectedEnd;
            }

            return messages;
        }

        private static int GetBodyLength(string rawData, int startIndex)
        {
            int lengthStart = rawData.IndexOf($"9=", startIndex);
            if (lengthStart < 0)
                return -1;

            lengthStart += 2; // Bỏ qua "9="
            int lengthEnd = rawData.IndexOf(Specs.DELIMITER_CHAR, lengthStart);
            if (lengthEnd < 0)
                return -1;

            string lengthStr = rawData.Substring(lengthStart, lengthEnd - lengthStart);
            return int.TryParse(lengthStr, out int length) ? length : -1;
        }
    }
}