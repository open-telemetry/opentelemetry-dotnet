// <copyright file="ReadmeGenerator.cs" company="OpenTelemetry Authors">
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

#nullable disable

using System.Text;
using RouteTests;

public class ReadmeGenerator
{
    public static void Main()
    {
        var sb = new StringBuilder();

        var testCases = RoutingTests.TestData;

        var results = new List<TestResult>();

        foreach (var item in testCases)
        {
            using var tests = new RoutingTests();
            var testCase = item[0] as RouteTestData.RouteTestCase;
            var result = tests.TestRoutes(testCase!).GetAwaiter().GetResult();
            results.Add(result);
        }

        sb.AppendLine("| | | display name | expected name (w/o http.method) | routing type | request |");
        sb.AppendLine("| - | - | - | - | - | - |");

        for (var i = 0; i < results.Count; ++i)
        {
            var result = results[i];
            var emoji = result.ActivityDisplayName.Equals(result.TestCase.ExpectedHttpRoute, StringComparison.InvariantCulture)
                ? ":green_heart:"
                : ":broken_heart:";
            sb.Append($"| {emoji} | [{i + 1}](#{i + 1}) ");
            sb.AppendLine(FormatTestResult(results[i]));
        }

        for (var i = 0; i < results.Count; ++i)
        {
            sb.AppendLine();
            sb.AppendLine($"#### {i + 1}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(results[i].RouteInfo.ToString());
            sb.AppendLine("```");
        }

        File.WriteAllText("README.md", sb.ToString());

        string FormatTestResult(TestResult result)
        {
            var testCase = result.TestCase!;

            return $"| {string.Join(
                " | ",
                result.ActivityDisplayName, // TODO: should be result.HttpRoute, but http.route is not currently added to Activity
                testCase.ExpectedHttpRoute,
                testCase.TestApplicationScenario,
                $"{testCase.HttpMethod} {testCase.Path}",
                result.ActivityDisplayName)} |";
        }
    }
}
