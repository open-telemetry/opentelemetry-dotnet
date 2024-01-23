// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RouteTests.TestApplication;

namespace RouteTests;

public static class RoutingTestCases
{
    public static IEnumerable<object[]> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("RoutingTestCases.json")!,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() },
            });
        return GetArgumentsFromTestCaseObject(input!);
    }

    private static IEnumerable<object[]> GetArgumentsFromTestCaseObject(IEnumerable<TestCase> input)
    {
        var result = new List<object[]>();

        foreach (var testCase in input)
        {
            if (testCase.MinimumDotnetVersion.HasValue && Environment.Version.Major < testCase.MinimumDotnetVersion.Value)
            {
                continue;
            }

            result.Add(new object[] { testCase });
        }

        return result;
    }

    public class TestCase
    {
        public string Name { get; set; } = string.Empty;

        public int? MinimumDotnetVersion { get; set; }

        public TestApplicationScenario TestApplicationScenario { get; set; }

        public string? HttpMethod { get; set; }

        public string Path { get; set; } = string.Empty;

        public int ExpectedStatusCode { get; set; }

        public string? ExpectedHttpRoute { get; set; }

        public string? CurrentHttpRoute { get; set; }

        public override string ToString()
        {
            // This is used by Visual Studio's test runner to identify the test case.
            return $"{this.TestApplicationScenario}: {this.Name}";
        }
    }
}
