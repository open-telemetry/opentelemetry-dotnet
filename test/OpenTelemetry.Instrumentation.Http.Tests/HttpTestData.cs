// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;

namespace OpenTelemetry.Instrumentation.Http.Tests;

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
        return value
            .Replace("{host}", host)
            .Replace("{port}", port.ToString())
            .Replace("{flavor}", "1.1");
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

        public Dictionary<string, string> SpanAttributes { get; set; }
    }
}
