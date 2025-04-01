// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.W3cTraceContext.Tests;

public sealed class W3CTraceContextTests : IDisposable
{
    /*
        To run the tests, invoke docker-compose.yml from the root of the repo:
        opentelemetry>docker compose --file=test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/docker-compose.yml --project-directory=. up --exit-code-from=tests --build
     */
    private const string W3CTraceContextEnvVarName = "OTEL_W3CTRACECONTEXT";
    private readonly HttpClient httpClient = new();
    private readonly ITestOutputHelper output;

    public W3CTraceContextTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Trait("CategoryName", "W3CTraceContextTests")]
    [SkipUnlessEnvVarFoundTheory(W3CTraceContextEnvVarName)]
    [InlineData("placeholder")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Need to use SkipUnlessEnvVarFoundTheory")]
    public void W3CTraceContextTestSuiteAsync(string value)
    {
        // configure SDK
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddAspNetCoreInstrumentation()
        .Build();

        var builder = WebApplication.CreateBuilder();
        using var app = builder.Build();

        app.MapPost("/", async ([FromBody] Data[] data) =>
        {
            var result = string.Empty;
            if (data != null)
            {
                foreach (var argument in data)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, argument.Url);
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(argument.Arguments),
                        Encoding.UTF8,
                        "application/json");
                    await this.httpClient.SendAsync(request);
                }
            }
            else
            {
                result = "done";
            }

            return result;
        });

        app.RunAsync("http://localhost:5000/");

        string result = RunCommand("python", "trace-context/test/test.py http://localhost:5000/");

        // Assert
        string lastLine = ParseLastLine(result);

        this.output.WriteLine("result:" + result);

        // Assert on the last line
        Assert.StartsWith("OK", lastLine, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    private static string RunCommand(string command, string args)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = $" {args}",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = ".",
            },
        };
        proc.Start();

        // TODO: after W3C Trace Context test suite passes, it might go in standard output
        var results = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return results;
    }

    private static string ParseLastLine(string output)
    {
        if (output.Length <= 1)
        {
            return output;
        }

        // The output ends with '\n', which should be ignored.
        var lastNewLineCharacterPos = output.LastIndexOf('\n', output.Length - 2);
        return output.Substring(lastNewLineCharacterPos + 1);
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    internal sealed class Data
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("arguments")]
        public Data[]? Arguments { get; set; }
    }
}
