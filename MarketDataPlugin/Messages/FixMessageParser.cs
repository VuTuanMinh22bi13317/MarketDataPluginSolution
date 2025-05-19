using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace MarketDataPlugin.Messages
{
    public class FixMessageParser
    {
        private readonly string _connectionString;

        public FixMessageParser(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task StoreToDatabaseAsync(string beginString, string msgType, XElement messageSpec, Dictionary<string, string> fields)
        {
            string tableName = messageSpec.Attribute("TableName")?.Value ?? "UnknownTable";
            
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var fieldsSpec = messageSpec.Descendants("Field").ToList();
                    var loopFields = messageSpec.Element("Loop")?.Elements("Field").ToList() ?? new List<XElement>();

                    var columns = new List<string>();
                    var parameters = new List<string>();
                    var sqlParameters = new List<SqlParameter>();

                    // Xử lý các field không thuộc loop
                    foreach (var field in fieldsSpec)
                    {
                        string? tag = field.Attribute("Tag")?.Value;
                        string? dataField = field.Attribute("DataField")?.Value ?? field.Attribute("Name")?.Value;
                        string type = field.Attribute("Type")?.Value ?? "String";

                        if (tag != null && fields.ContainsKey(tag) && dataField != null)
                        {
                            columns.Add(dataField);
                            string paramName = $"@p_{tag}";
                            parameters.Add(paramName);
                            sqlParameters.Add(new SqlParameter(paramName, ConvertFieldValue(fields[tag], type)));
                        }
                    }

                    // Xử lý loop nếu có
                    if (loopFields.Any())
                    {
                        string? noEntriesTag = messageSpec.Element("Loop")?.Attribute("NoMDEntriesTag")?.Value;
                        if (noEntriesTag != null && fields.ContainsKey(noEntriesTag) && int.TryParse(fields[noEntriesTag], out int noEntries))
                        {
                            for (int i = 1; i <= noEntries; i++)
                            {
                                foreach (var field in loopFields)
                                {
                                    string? tag = field.Attribute("Tag")?.Value;
                                    string? dataField = field.Attribute("DataField")?.Value ?? field.Attribute("Name")?.Value;
                                    string type = field.Attribute("Type")?.Value ?? "String";

                                    string loopKey = $"{tag}-{i}";
                                    if (tag != null && fields.ContainsKey(loopKey) && dataField != null)
                                    {
                                        columns.Add($"{dataField}_{i}");
                                        string paramName = $"@p_{tag}_{i}";
                                        parameters.Add(paramName);
                                        sqlParameters.Add(new SqlParameter(paramName, ConvertFieldValue(fields[loopKey], type)));
                                    }
                                }
                            }
                        }
                    }

                    string commandText = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
                    
                    using (var command = new SqlCommand(commandText, connection))
                    {
                        command.Parameters.AddRange(sqlParameters.ToArray());
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex)
            {
                throw new Exception($"Failed to store message in database: {ex.Message}", ex);
            }
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