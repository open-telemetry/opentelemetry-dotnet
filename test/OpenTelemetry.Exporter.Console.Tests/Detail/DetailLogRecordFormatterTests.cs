// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Detail;

public class DetailLogRecordFormatterTests
{
    [Fact]
    public void DetailFormatTest()
    {
        // Includes full output, with timestamp, event, trace ID, and parameters

        // Arrange
        var mockConsole = new MockConsole();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "detail";
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<DetailLogRecordFormatterTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        // Use high performance logging
        logger.HelloFrom("tomato", 2.99);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Log: {0}", output);

        // Check has Trace ID
        Assert.Matches(@"LogRecord.TraceId:\s+[0-9a-f]{32}", output);

        // Check has severity
        Assert.Matches(@"LogRecord.Severity:\s+Info", output);

        // Check has Event name
        Assert.Matches(@"LogRecord.EventName:\s+HelloFrom", output);

        // Check contains the message with parameters
        Assert.Matches(@"LogRecord.FormattedMessage:\s+Hello from tomato 2.99.", output);
    }

    [Fact]
    public void AdditionalAttributesLogRecordTest()
    {
        // Includes full output, with timestamp, event, trace ID, and parameters

        // Arrange
        var mockConsole = new MockConsole();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var resourceAttributes = new Dictionary<string, object>() { { "att1", "val1" } };
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder
                    .CreateDefault()
                    .AddService("console-test")
                    .AddAttributes(resourceAttributes));
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "detail";
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<DetailLogRecordFormatterTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

#pragma warning disable CA1848
        var scope = logger.BeginScope("Scope {Arg1}", "alpha");
#pragma warning restore CA1848

        logger.TestWarn("warning1");

        Exception ex;
        try
        {
            throw new InvalidOperationException("Something went wrong!");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        logger.ExceptionTest(ex);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Log: {0}", output);

        Assert.Matches(@"att1:\s+val1", output);
        Assert.Matches(@"LogRecord.Severity:\s+Warn", output);
        Assert.Matches(@"LogRecord.Severity:\s+Fatal", output);
        Assert.Matches(@"Something went wrong", output);
        Assert.Matches(@"Arg1:\s+alpha", output);
    }
}
