// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Text;
using Microsoft.AspNetCore.Builder;
using RouteTests.TestApplication;

namespace RouteTests;

public class RoutingTestFixture : IDisposable
{
    private static readonly HttpClient HttpClient = new();
    private readonly Dictionary<TestApplicationScenario, WebApplication> apps = new();
    private readonly RouteInfoDiagnosticObserver diagnostics = new();
    private readonly List<RoutingTestResult> testResults = new();

    public RoutingTestFixture()
    {
        foreach (var scenario in Enum.GetValues<TestApplicationScenario>())
        {
            var app = TestApplicationFactory.CreateApplication(scenario);
            if (app != null)
            {
                this.apps.Add(scenario, app);
            }
        }

        foreach (var app in this.apps)
        {
            app.Value.RunAsync();
        }
    }

    public async Task MakeRequest(TestApplicationScenario scenario, string path)
    {
        var app = this.apps[scenario];
        var baseUrl = app.Urls.First();
        var url = $"{baseUrl}{path}";
        await HttpClient.GetAsync(url);
    }

    public void AddTestResult(RoutingTestResult result)
    {
        this.testResults.Add(result);
    }

    public void Dispose()
    {
        foreach (var app in this.apps)
        {
            app.Value.DisposeAsync().GetAwaiter().GetResult();
        }

        HttpClient.Dispose();
        this.diagnostics.Dispose();

        this.GenerateReadme();
    }

    private void GenerateReadme()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Test results for ASP.NET Core {Environment.Version.Major}");
        sb.AppendLine();
        sb.AppendLine("| http.route | App | Test Name |");
        sb.AppendLine("| - | - | - |");

        for (var i = 0; i < this.testResults.Count; ++i)
        {
            var result = this.testResults[i];
            var emoji = result.TestCase.CurrentHttpRoute == null ? ":green_heart:" : ":broken_heart:";
            sb.AppendLine($"| {emoji} | {result.TestCase.TestApplicationScenario} | [{result.TestCase.Name}]({MakeAnchorTag(result.TestCase.TestApplicationScenario, result.TestCase.Name)}) |");
        }

        for (var i = 0; i < this.testResults.Count; ++i)
        {
            var result = this.testResults[i];
            sb.AppendLine();
            sb.AppendLine($"## {result.TestCase.TestApplicationScenario}: {result.TestCase.Name}");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(result.ToString());
            sb.AppendLine("```");
        }

        var readmeFileName = $"README.net{Environment.Version.Major}.0.md";
        File.WriteAllText(Path.Combine("..", "..", "..", "RouteTests", readmeFileName), sb.ToString());

        static string MakeAnchorTag(TestApplicationScenario scenario, string name)
        {
            var chars = name.ToCharArray()
                .Where(c => !char.IsPunctuation(c) || c == '-')
                .Select(c => c switch
                {
                    '-' => '-',
                    ' ' => '-',
                    _ => char.ToLower(c),
                })
                .ToArray();

            return $"#{scenario.ToString().ToLower()}-{new string(chars)}";
        }
    }
}