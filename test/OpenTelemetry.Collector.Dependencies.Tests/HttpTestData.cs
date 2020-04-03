// <copyright file="HttpTestData.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenTelemetry.Collector.Dependencies.Tests
{
    public static class HttpTestData
    {
        public class HttpOutTestCase
        {
            public string Name { get; set; }

            public string Method { get; set; }

            public string Url { get; set; }

            public Dictionary<string, string> Headers { get; set; }

            public int ResponseCode { get; set; }

            public string SpanName { get; set; }

            public string SpanKind { get; set; }

            public string SpanStatus { get; set; }

            public bool? SpanStatusHasDescription { get; set; }

            public Dictionary<string, string> SpanAttributes { get; set; }

            public bool SetHttpFlavor { get; set; }
        }

        public static IEnumerable<object[]> ReadTestCases()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpOutTestCase[]>(new JsonTextReader(new StreamReader(assembly.GetManifestResourceStream("OpenTelemetry.Collector.Dependencies.Tests.http-out-test-cases.json"))));

            return GetArgumentsFromTestCaseObject(input);
        }

        public static IEnumerable<object[]> GetArgumentsFromTestCaseObject(IEnumerable<HttpOutTestCase> input)
        {
            var result = new List<object[]>();

            foreach (var testCase in input)
            {
                result.Add(new object[] {
                    testCase,
                });
            }

            return result;
        }

        public static string NormalizeValues(string value, string host, int port)
        {
            return value.Replace("{host}", host).Replace("{port}", port.ToString());
        }
    }
}
