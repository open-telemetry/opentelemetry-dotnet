// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

public class CompactActivityFormatterTests
{
    [Fact]
    public async Task BasicActivityOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .ConfigureResource(res => res.AddService("console-test"))
            .AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                })
            .Build();

        // Act
        using var activitySource = new ActivitySource("TestActivitySource");
        var traceId = default(string);
        using (var activity1 = activitySource.StartActivity("TestActivity"))
        {
            traceId = activity1?.TraceId.ToHexString();

            await Task.Delay(100);
        }

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Output: {0}", output);

        // Contains trace ID and span ID
        Assert.Matches(@"^SPAN \[TestActivity\] [0-9a-f]{32}-[0-9a-f]{16}", output);
        Assert.NotNull(traceId);
        Assert.Contains(traceId, output, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task MultipleActivityOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("OuterActivitySource", "InnerActivitySource")
            .ConfigureResource(res => res.AddService("console-test"))
            .AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                })
            .Build();

        // Act
        using var activitySource = new ActivitySource("OuterActivitySource");
        var traceId = default(string);

        using (var activity1 = activitySource.StartActivity("OuterActivity"))
        {
            traceId = activity1?.TraceId.ToHexString();

            await Task.Delay(100);

            using var activitySource2 = new ActivitySource("InnerActivitySource");
            using (var activity2 = activitySource2.StartActivity("InnerActivity"))
            {
                await Task.Delay(200);
            }

            await Task.Delay(400);
        }

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Output: {0}", output);

        // Inner Activity completes first, so should be output first
        Assert.Matches(@"^SPAN \[InnerActivity\]", output);
        Assert.Matches(@"SPAN \[OuterActivity\]", output);
        Assert.NotNull(traceId);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var values0 = lines[0].Split(' ');
        Assert.Contains(traceId, values0[2], StringComparison.InvariantCulture);
        var values1 = lines[1].Split(' ');
        Assert.Contains(traceId, values1[2], StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ErrorActivityOutputTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestActivitySource")
            .ConfigureResource(res => res.AddService("console-test"))
            .AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                })
            .Build();

        // Act
        using var activitySource = new ActivitySource("TestActivitySource");
        using (var activity1 = activitySource.StartActivity("TestActivity"))
        {
            await Task.Delay(100);
            activity1?.SetStatus(ActivityStatusCode.Error, "Failed");
        }

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Output: {0}", output);

        // Contains ERROR indicator
        Assert.Matches(@"^SPAN ERROR", output);
    }
}
