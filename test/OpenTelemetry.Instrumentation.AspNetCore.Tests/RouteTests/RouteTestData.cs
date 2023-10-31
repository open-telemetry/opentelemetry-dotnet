// <copyright file="RouteTestData.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RouteTests;

public static class RouteTestData
{
    public static IEnumerable<object[]> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<RouteTestCase[]>(
            assembly.GetManifestResourceStream("RouteTests.testcases.json")!,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() },
            });
        return GetArgumentsFromTestCaseObject(input!);
    }

    private static IEnumerable<object[]> GetArgumentsFromTestCaseObject(IEnumerable<RouteTestCase> input)
    {
        var result = new List<object[]>();

        if (input.Any(x => x.Debug))
        {
            foreach (var testCase in input.Where(x => x.Debug))
            {
                result.Add(new object[] { testCase, true });
            }
        }
        else
        {
            foreach (var testCase in input)
            {
                if (testCase.MinimumDotnetVersion.HasValue && Environment.Version.Major < testCase.MinimumDotnetVersion.Value)
                {
                    continue;
                }

                result.Add(new object[] { testCase, true });
                result.Add(new object[] { testCase, false });
            }
        }

        return result;
    }

    public class RouteTestCase
    {
        public string Name { get; set; } = string.Empty;

        public int? MinimumDotnetVersion { get; set; }

        public bool Debug { get; set; }

        public TestApplicationScenario TestApplicationScenario { get; set; }

        public string? HttpMethod { get; set; }

        public string Path { get; set; } = string.Empty;

        public int ExpectedStatusCode { get; set; }

        public string? ExpectedHttpRoute { get; set; }

        public string? CurrentActivityDisplayName { get; set; }

        public string? CurrentActivityHttpRoute { get; set; }

        public string? CurrentMetricHttpRoute { get; set; }

        public override string ToString()
        {
            return $"{this.TestApplicationScenario}: {this.Name}";
        }
    }
}
