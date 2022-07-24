// <copyright file="HttpTestData.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
    public static class HttpTestData
    {
        public static IEnumerable<object[]> ReadTestCases()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var input = JsonSerializer.Deserialize<HttpOutTestCase[]>(
                assembly.GetManifestResourceStream("OpenTelemetry.Instrumentation.Http.Tests.http-out-test-cases.json"), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return GetArgumentsFromTestCaseObject(input);
        }

        public static IEnumerable<object[]> GetArgumentsFromTestCaseObject(IEnumerable<HttpOutTestCase> input)
        {
            var result = new List<object[]>();

            foreach (var testCase in input)
            {
                result.Add(new object[]
                {
                    testCase,
                });
            }

            return result;
        }

        public static string NormalizeValues(string value, string host, int port)
        {
            return value.Replace("{host}", host).Replace("{port}", port.ToString());
        }

        public class HttpOutTestCase
        {
            public string Name { get; set; }

            public string Method { get; set; }

            public string Url { get; set; }

            public Dictionary<string, string> Headers { get; set; }

            public int ResponseCode { get; set; }

            public string SpanName { get; set; }

            public bool ResponseExpected { get; set; }

            public bool? RecordException { get; set; }

            public string SpanStatus { get; set; }

            public bool? SpanStatusHasDescription { get; set; }

            [JsonConverter(typeof(DictionaryStringStringConverter))]
            public Dictionary<string, string> SpanAttributes { get; set; }
        }

        /// <summary>
        /// Convert string-number to string-string pair.
        /// </summary>
        private class DictionaryStringStringConverter : JsonConverter<Dictionary<string, string>>
        {
            public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException("The token type should be StartObject.");
                }

                var value = new Dictionary<string, string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return value;
                    }

                    var key = reader.GetString();

                    reader.Read();
                    string valueAsString;
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        valueAsString = reader.GetInt32().ToString();
                    }
                    else
                    {
                        valueAsString = reader.GetString();
                    }

                    value.Add(key, valueAsString);
                }

                throw new JsonException("Json Token is not valid.");
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                foreach (var item in value)
                {
                    writer.WriteString(item.Key, item.Value);
                }

                writer.WriteEndObject();
            }
        }
    }
}
