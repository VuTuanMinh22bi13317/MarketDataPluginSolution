using MarketDataPlugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketDataPlugin.Parsers
{
    public class FixParser
    {
        private readonly Specs specs;

        public FixParser(string specsFile, string majorVersion, string minorVersion)
        {
            specs = new Specs(majorVersion, minorVersion);
            specs.Load(specsFile);
        }

        public void Parse(string fixMessage)
        {
            if (string.IsNullOrEmpty(fixMessage))
            {
                Console.WriteLine("Error: FIX message is empty or null.");
                return;
            }

            var fields = ParseFixMessage(fixMessage);

            if (!fields.TryGetValue("8", out string? beginString) || beginString != specs.BeginString)
            {
                Console.WriteLine($"Error: Invalid BeginString. Expected {specs.BeginString}, but got {beginString}.");
                return;
            }

            if (!fields.TryGetValue("35", out string? msgType))
            {
                Console.WriteLine("Error: MsgType (Tag 35) not found in FIX message.");
                return;
            }

            if (!specs.TryGetMessage(msgType, out MsgDef? msgDef))
            {
                Console.WriteLine($"Error: MsgType '{msgType}' not found in Specs.");
                return;
            }

            if (msgDef == null)
            {
                Console.WriteLine($"Error: Message definition for MsgType '{msgType}' is null.");
                return;
            }

            Console.WriteLine($"\nParsing FIX Message: MsgType={msgType} ({msgDef.Name})");

            Console.WriteLine("Header Fields:");
            ParseFields(fields, specs.HeaderFields, 0);

            Console.WriteLine("Message Fields:");
            ParseFields(fields, msgDef.GetChildren(), 0);
        }

        private Dictionary<string, string> ParseFixMessage(string fixMessage)
        {
            var fields = new Dictionary<string, string>();
            var pairs = fixMessage.Split('|', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length != 2) continue;
                fields[parts[0]] = parts[1];
            }

            return fields;
        }

        private void ParseFields(Dictionary<string, string> fields, IReadOnlyDictionary<string, FieldDef>? headerFields, int indentLevel)
        {
            if (headerFields == null) return;

            string indent = new string(' ', indentLevel * 2);

            foreach (var field in headerFields)
            {
                var fieldDef = field.Value;
                if (fields.TryGetValue(fieldDef.Tag, out string? value))
                {
                    string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                        ? $", DateFormat={fieldDef.DateFormat}"
                        : "";
                    Console.WriteLine($"{indent}Field: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}{dateFormatDisplay}, Value={value}");
                }
            }
        }

        private void ParseFields(Dictionary<string, string> fields, IReadOnlyDictionary<string, object>? children, int indentLevel)
        {
            if (children == null) return;

            string indent = new string(' ', indentLevel * 2);

            foreach (var child in children)
            {
                if (child.Value is FieldDef fieldDef)
                {
                    if (fields.TryGetValue(fieldDef.Tag, out string? value))
                    {
                        string dateFormatDisplay = fieldDef.Type == FieldType.Date && !string.IsNullOrEmpty(fieldDef.DateFormat)
                            ? $", DateFormat={fieldDef.DateFormat}"
                            : "";
                        Console.WriteLine($"{indent}Field: Tag={fieldDef.Tag}, Name={fieldDef.Name}, Type={fieldDef.Type}, DataField={fieldDef.DataField}{dateFormatDisplay}, Value={value}");
                    }
                }
                else if (child.Value is LoopDef loopDef)
                {
                    if (!string.IsNullOrEmpty(loopDef.Counter) && fields.TryGetValue(loopDef.Counter, out string? counterValue))
                    {
                        if (int.TryParse(counterValue, out int count))
                        {
                            Console.WriteLine($"{indent}Loop: Name={loopDef.Name}, Counter={loopDef.Counter}, Count={count}");
                            for (int i = 0; i < count; i++)
                            {
                                Console.WriteLine($"{indent}  Entry {i + 1}:");
                                ParseFields(fields, loopDef.GetChildren(), indentLevel + 2);
                            }
                        }
                    }
                }
            }
        }
    }
}