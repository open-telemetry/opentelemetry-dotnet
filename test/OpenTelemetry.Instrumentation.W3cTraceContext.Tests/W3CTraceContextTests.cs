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
    public async Task W3CTraceContextTestSuiteAsync(string value)
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

        _ = app.RunAsync("http://localhost:5000/");

        (var stdout, var stderr) = await RunCommand("python", "-W ignore trace-context/test/test.py http://localhost:5000/");

        // Assert
        // TODO: after W3C Trace Context test suite passes, it might go in standard output
        string lastLine = ParseLastLine(stderr);

        this.output.WriteLine("[stderr]" + stderr);
        this.output.WriteLine("[stdout]" + stdout);

        // Assert on the last line
        Assert.StartsWith("OK", lastLine, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    private static async Task<(string StdOut, string StdErr)> RunCommand(string command, string args)
    {
        using var process = new Process
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
        process.Start();

        // See https://stackoverflow.com/a/16326426/1064169 and
        // https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput.
        using var outputTokenSource = new CancellationTokenSource();

        var readOutput = ReadOutputAsync(process, outputTokenSource.Token);

        try
        {
            await process.WaitForExitAsync();
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        finally
        {
            await outputTokenSource.CancelAsync();
        }

        try
        {
            return await readOutput;
        }
        finally
        {
            process.Dispose();
            outputTokenSource.Dispose();
        }
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

    private static async Task<(string Output, string Error)> ReadOutputAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        var processErrors = ConsumeStreamAsync(process.StandardError, process.StartInfo.RedirectStandardError, cancellationToken);
        var processOutput = ConsumeStreamAsync(process.StandardOutput, process.StartInfo.RedirectStandardOutput, cancellationToken);

        await Task.WhenAll(processErrors, processOutput);

        string error = string.Empty;
        string output = string.Empty;

        if (processErrors.Status == TaskStatus.RanToCompletion)
        {
            error = (await processErrors).ToString();
        }

        if (processOutput.Status == TaskStatus.RanToCompletion)
        {
            output = (await processOutput).ToString();
        }

        return (output, error);
    }

    private static Task<StringBuilder> ConsumeStreamAsync(
        StreamReader reader,
        bool isRedirected,
        CancellationToken cancellationToken)
    {
        return isRedirected ?
            Task.Run(() => ProcessStream(reader, cancellationToken), cancellationToken) :
            Task.FromResult(new StringBuilder(0));

        static async Task<StringBuilder> ProcessStream(
            StreamReader reader,
            CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();

            try
            {
                builder.Append(await reader.ReadToEndAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }

            return builder;
        }
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
