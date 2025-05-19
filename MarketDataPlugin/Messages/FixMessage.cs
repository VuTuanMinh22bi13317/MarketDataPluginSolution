using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace MarketDataPlugin.Messages
{
    public class FixMessage
    {
        public string BeginString { get; }
        public string MsgType { get; }
        public XElement MessageSpec { get; }
        public readonly Dictionary<string, string> Fields = new Dictionary<string, string>();

        public FixMessage(string beginString, string msgType, XElement messageSpec)
        {
            BeginString = beginString;
            MsgType = msgType;
            MessageSpec = messageSpec;
        }

        public (string TableName, string CommandText, List<SqlParameter> Parameters) PrepareInsertCommand()
        {
            string tableName = MessageSpec.Attribute("TableName")?.Value ?? "UnknownTable";
            var fields = MessageSpec.Descendants("Field").ToList();
            var loopFields = MessageSpec.Element("Loop")?.Elements("Field").ToList() ?? new List<XElement>();

            var columns = new List<string>();
            var parameters = new List<string>();
            var sqlParameters = new List<SqlParameter>();

            // Xử lý các field không thuộc loop
            foreach (var field in fields)
            {
                string? tag = field.Attribute("Tag")?.Value;
                string? dataField = field.Attribute("DataField")?.Value ?? field.Attribute("Name")?.Value;
                string type = field.Attribute("Type")?.Value ?? "String";

                if (tag != null && Fields.ContainsKey(tag) && dataField != null)
                {
                    columns.Add(dataField);
                    string paramName = $"@p_{tag}";
                    parameters.Add(paramName);
                    sqlParameters.Add(new SqlParameter(paramName, ConvertFieldValue(Fields[tag], type)));
                }
            }

            // Xử lý loop nếu có
            if (loopFields.Any())
            {
                string? noEntriesTag = MessageSpec.Element("Loop")?.Attribute("NoMDEntriesTag")?.Value;
                if (noEntriesTag != null && Fields.ContainsKey(noEntriesTag) && int.TryParse(Fields[noEntriesTag], out int noEntries))
                {
                    for (int i = 1; i <= noEntries; i++)
                    {
                        foreach (var field in loopFields)
                        {
                            string? tag = field.Attribute("Tag")?.Value;
                            string? dataField = field.Attribute("DataField")?.Value ?? field.Attribute("Name")?.Value;
                            string type = field.Attribute("Type")?.Value ?? "String";

                            if (tag != null && Fields.ContainsKey($"{tag}-{i}") && dataField != null)
                            {
                                columns.Add($"{dataField}_{i}");
                                string paramName = $"@p_{tag}_{i}";
                                parameters.Add(paramName);
                                sqlParameters.Add(new SqlParameter(paramName, ConvertFieldValue(Fields[$"{tag}-{i}"], type)));
                            }
                        }
                    }
                }
            }

            string commandText = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
            return (tableName, commandText, sqlParameters);
        }

        private object ConvertFieldValue(string value, string type)
        {
            switch (type.ToLower())
            {
                case "int":
                    return int.TryParse(value, out int intValue) ? intValue : 0;
                case "double":
                    return double.TryParse(value.Replace(",", "."), out double doubleValue) ? doubleValue : 0.0;
                case "datetime":
                case "date":
                    return DateTime.TryParse(value, out DateTime dateValue) ? dateValue : DateTime.MinValue;
                case "string":
                default:
                    return value;
            }
        }
    }
}