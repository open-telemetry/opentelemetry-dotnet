// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

public class CompactLogRecordFormatterTests
{
    [Fact]
    public void FullIntegrationTest()
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
                    configure.Formatter = "compact";
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        // Use high performance logging
        logger.HelloFrom("tomato", 2.99);

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Output: {0}", output);

        // Check first line has timestamp, severity, event, trace ID, span ID
        Assert.Matches(
            @"^\d\d:\d\d:\d\d INFO \[HelloFrom\] [0-9a-f]{32}-[0-9a-f]{16}",
            output);

        // Check second line contains the message with parameters
        Assert.Contains(
            $"Hello from tomato 2.99.",
            output,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public void SeverityColourChangeTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();

#pragma warning disable CA1848
        logger.LogInformation("Test log message from SimpleConsole exporter");
#pragma warning restore CA1848

        // Assert
        var output = mockConsole.GetOutput();

        // First line should NOT contain trace ID when no activity is present
        Assert.Matches(@"^\d\d:\d\d:\d\d INFO", output);
        Assert.Contains($"Test log message from SimpleConsole exporter", output, StringComparison.InvariantCulture);

        // Verify color changes: fg and bg for severity, then restore both
        Assert.Equal(8, mockConsole.ColorChanges.Count);
        Assert.Equal(("Foreground", ConsoleColor.DarkGreen), mockConsole.ColorChanges[0]); // Severity fg
        Assert.Equal(("Background", ConsoleColor.Black), mockConsole.ColorChanges[1]); // Severity bg
        Assert.Equal(("Foreground", MockConsole.DefaultForeground), mockConsole.ColorChanges[2]); // Restore fg
        Assert.Equal(("Background", MockConsole.DefaultBackground), mockConsole.ColorChanges[3]); // Restore bg

        // Then change colour to white for message body
        Assert.Equal(("Foreground", ConsoleColor.White), mockConsole.ColorChanges[4]); // Message fg
        Assert.Equal(("Background", ConsoleColor.Black), mockConsole.ColorChanges[5]); // Message bg
        Assert.Equal(("Foreground", MockConsole.DefaultForeground), mockConsole.ColorChanges[6]); // Restore fg
        Assert.Equal(("Background", MockConsole.DefaultBackground), mockConsole.ColorChanges[7]); // Restore bg
    }

    [Fact]
    public void SourceGeneratedLoggerMessage()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();

        logger.HelloFrom("tomato", 2.99);

        // Assert
        var output = mockConsole.GetOutput();

        // Should contain the automaticsource generated event name
        Assert.StartsWith(@"INFO [HelloFrom]", output, StringComparison.InvariantCulture);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "Event1", "Trace message", "TRACE")]
    [InlineData(LogLevel.Debug, "Event2", "Debug message", "DEBUG")]
    [InlineData(LogLevel.Information, "Event10", "Info message", "INFO")]
    [InlineData(LogLevel.Warning, "Event100", "Warning message", "WARN")]
    [InlineData(LogLevel.Error, "Event500", "Error message", "ERROR")]
    [InlineData(LogLevel.Critical, "Event999", "Critical message", "FATAL")]
    public void LogLevelAndFormatTheoryTest(
        LogLevel logLevel,
        string? eventName,
        string message,
        string expectedSeverity)
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging
                .SetMinimumLevel(LogLevel.Trace)
                .AddOpenTelemetry(options =>
                {
                    options.AddConsoleExporter(configure =>
                    {
                        configure.Formatter = "compact";
                        configure.TimestampFormat = string.Empty;
                        configure.Console = mockConsole;
                    });
                }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
#pragma warning disable CA2254 // Template should be a static string
#pragma warning disable CA1848
        logger.Log(logLevel, new EventId(0, eventName), message);
#pragma warning restore CA1848
#pragma warning restore CA2254 // Template should be a static string

        // Assert
        var output = mockConsole.GetOutput();

        Assert.StartsWith(
            $"{expectedSeverity} [{eventName}]",
            output,
            StringComparison.InvariantCulture);
        Assert.Contains($"{message}", output, StringComparison.InvariantCulture);
    }

    [Fact]
    public void StructuredLoggingWithSemanticArgumentsTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging
                .SetMinimumLevel(LogLevel.Trace)
                .AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.AddConsoleExporter(configure =>
                    {
                        configure.Formatter = "compact";
                        configure.TimestampFormat = string.Empty;
                        configure.Console = mockConsole;
                    });
                }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
        var userName = "Alice";
        var userId = 12345;
#pragma warning disable CA1848
        logger.LogInformation("User {UserName} with ID {UserId} logged in", userName, userId);
#pragma warning restore CA1848

        // Assert
        var output = mockConsole.GetOutput();

        Assert.StartsWith("INFO", output, StringComparison.InvariantCulture);
        Assert.Contains($"User Alice with ID 12345 logged in", output, StringComparison.InvariantCulture);
    }

    [Fact]
    public void ExceptionLogTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
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
        Assert.StartsWith("FATAL [ExceptionTest]", output, StringComparison.InvariantCulture);
        Assert.Contains($"This is a critical error with exception", output, StringComparison.InvariantCulture);

        Assert.Contains(
            "System.InvalidOperationException: Something went wrong!",
            output,
            StringComparison.InvariantCulture);

        // Should contain at least one stack trace line, indented
        Assert.Contains("  at ", output, StringComparison.InvariantCulture);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TimestampOutputTest(bool useUtc)
    {
        // Arrange
        var mockConsole = new MockConsole();
        var timestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.Console = mockConsole;
                    configure.TimestampFormat = timestampFormat;
                    configure.UseUtcTimestamp = useUtc;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
        var before = useUtc ? DateTimeOffset.UtcNow : DateTimeOffset.Now;

        logger.TestLog("Timestamped log message");

        // Assert
        var output = mockConsole.GetOutput();

        var match = Regex.Match(output, @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) INFO \[TestLog\]");
        Assert.True(match.Success, "Timestamp not found in the correct format.");

        var timestampStr = match.Groups[1].Value;
        DateTimeOffset parsedTimestamp;

        if (useUtc)
        {
            // For UTC, parse as UTC and ensure offset is zero
            parsedTimestamp = DateTimeOffset.ParseExact(
                timestampStr,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            Assert.Equal(TimeSpan.Zero, parsedTimestamp.Offset);
        }
        else
        {
            // For local, parse as local and ensure offset matches local system
            parsedTimestamp = DateTimeOffset.ParseExact(
                timestampStr,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal);
            Assert.Equal(before.Offset, parsedTimestamp.Offset);
        }

        // Check that the parsed timestamp is within the expected range (with a tolerance of 5 seconds)
        var timeDifference = (parsedTimestamp - before).Duration();
        Assert.True(
            timeDifference.TotalSeconds < 5,
            $"Timestamp is not within the expected range. Difference: {timeDifference.TotalSeconds} seconds.");
    }

    [Fact]
    public void ActivityContextOutputTest()
    {
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
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();
        using var activitySource = new ActivitySource("TestActivitySource");
        using var activity = activitySource.StartActivity("TestActivity");

        // Log the activity ID in the message
        logger.TestLog($"Activity {activity?.Id} started");

        // Assert
        var output = mockConsole.GetOutput();
        System.Console.WriteLine("Output: {0}", output);

        // Contains trace ID and span ID
        Assert.Matches(@"^INFO \[TestLog\] [0-9a-f]{32}-[0-9a-f]{16}", output);

        // Contains the activity message
        Assert.Matches(@"Message: Activity 00-[0-9a-f]{32}-[0-9a-f]{16}-00 started", output);
    }

    [Fact]
    public void NumericOnlyEventTest()
    {
        // Arrange
        var mockConsole = new MockConsole();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                });
            }));

        // Act
        var logger = loggerFactory.CreateLogger<CompactLogRecordFormatterTests>();

#pragma warning disable CA1848
        logger.Log(LogLevel.Information, 1234, "Test log message from SimpleConsole exporter");
#pragma warning restore CA1848

        // Assert
        var output = mockConsole.GetOutput();

        Assert.StartsWith(@"INFO [1234]", output, StringComparison.InvariantCulture);
    }
}
