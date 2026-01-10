// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;
#if NET8_0_OR_GREATER
using TimeProvider = System.TimeProvider;
#endif

namespace OpenTelemetry.Exporter.Console.Tests.Compact;

/// <summary>
/// Threading tests for SimpleConsoleExporter.
/// </summary>
public class ThreadingTests
{
#if NETCOREAPP2_1_OR_GREATER
    private static readonly Func<string, string, StringComparison, bool> Contains = (s, value, comparison) => s.Contains(value, comparison);
#else
    private static readonly Func<string, string, StringComparison, bool> Contains = (s, value, comparison) => s.IndexOf(value, comparison) >= 0;
#endif

    [Fact]
    public void SingleThread_SingleMessage_WritesCorrectly()
    {
        // Arrange
        var mockConsole = new ThreadingTestMockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter(configure =>
                {
                    configure.Formatter = "compact";
                    configure.TimestampFormat = string.Empty;
                    configure.Console = mockConsole;
                });
            }));

        var logger = loggerFactory.CreateLogger<ThreadingTests>();

        // Act
        logger.TestLog("Single thread");

        // Assert
        var calls = mockConsole.Calls.ToArray();
        var fgIndex = Array.FindIndex(calls, c => c.StartsWith("Foreground:", StringComparison.Ordinal));
        Assert.True(fgIndex >= 0, "Should have a Foreground color call");
        Assert.True(calls.Length > fgIndex + 4, "Should have enough calls after Foreground");
        Assert.StartsWith("Write:INFO", calls[fgIndex + 2], StringComparison.InvariantCulture);
        Assert.StartsWith("Foreground:", calls[fgIndex + 3], StringComparison.InvariantCulture); // restore fg
        Assert.StartsWith("Background:", calls[fgIndex + 4], StringComparison.InvariantCulture); // restore bg
    }

    [Fact]
    public async Task TwoThreads_MessagesAreNotInterleaved()
    {
        // Arrange
        var mockConsole = new ThreadingTestMockConsole();
        using var loggerFactory = LoggerFactory.Create(logging => logging
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

        var loggerInfo = loggerFactory.CreateLogger("ThreadingTests.LoggerInfo");
        var loggerWarn = loggerFactory.CreateLogger("ThreadingTests.LoggerWarn");
        var loggerFail = loggerFactory.CreateLogger("ThreadingTests.LoggerFail");
#if NET8_0_OR_GREATER
        var timeProvider = TimeProvider.System;
        Func<DateTimeOffset> getUtcNow = () => timeProvider.GetUtcNow();
#else
        Func<DateTimeOffset> getUtcNow = () => DateTimeOffset.UtcNow;
#endif

        // Planned schedule:
        // t=0, Warning message log starts, it should be written first
        // t=400, Info message logs, with a delay to make sure it happens after the Warning
        // t=800, Error message logs, with a delay
        // t=900, Warning message completes after delays for the colour, severity, and text
        // t=915, Info message completes (it only has 15ns delay, but should wait until after the warning)
        // t=930, Error message completes

        System.Console.WriteLine($"Start: {getUtcNow():HH:mm:ss.fff}");

        var t1 = Task.Run(async () =>
        {
            await Task.Delay(400);
            loggerInfo.TestLog($"Info message second {getUtcNow():HH:mm:ss.fff}");
        });
        var t2 = Task.Run(async () =>
        {
            await Task.Delay(0);
            loggerWarn.TestWarn($"Warning message first {getUtcNow():HH:mm:ss.fff}");
        });
        var t3 = Task.Run(async () =>
        {
            await Task.Delay(800);
            loggerFail.TestError($"Error message third {getUtcNow():HH:mm:ss.fff}");
        });

        await Task.WhenAll(t1, t2, t3);

        System.Console.WriteLine($"End: {getUtcNow():HH:mm:ss.fff}");

        // Assert
        var calls = mockConsole.Calls.ToArray();

        // Find the first info and last write
        var warnStart = Array.FindIndex(calls, c => Contains(c, "foreground:Yellow", StringComparison.OrdinalIgnoreCase));
        var warnEnd = Array.FindIndex(calls, c => Contains(c, "Warning message first", StringComparison.OrdinalIgnoreCase));
        var infoStart = Array.FindIndex(calls, c => Contains(c, "foreground:DarkGreen", StringComparison.OrdinalIgnoreCase));
        var infoEnd = Array.FindIndex(calls, c => Contains(c, "Info message second", StringComparison.OrdinalIgnoreCase));
        var failStart = Array.FindIndex(calls, c => Contains(c, "foreground:Black", StringComparison.OrdinalIgnoreCase));
        var failEnd = Array.FindIndex(calls, c => Contains(c, "Error message third", StringComparison.OrdinalIgnoreCase));

        System.Console.WriteLine($"warn: {warnStart}-{warnEnd}, info: {infoStart}-{infoEnd}, fail: {failStart}-{failEnd}");
        System.Console.WriteLine(string.Join("\n", calls));

        Assert.True(infoStart >= 0, "Should have info messages");
        Assert.True(warnStart >= 0, "Should have warn messages");
        Assert.True(failStart >= 0, "Should have fail messages");

        Assert.True(warnStart < warnEnd, "Warn message should be in order");
        Assert.True(infoStart < infoEnd, "Info message should be in order");
        Assert.True(failStart < failEnd, "Fail message should be in order");

        Assert.True(warnEnd < infoStart, "Warn should end before info starts");
        Assert.True(infoEnd < failStart, "Info should end before fail starts");
    }

    internal sealed class ThreadingTestMockConsole : IConsole
    {
#if NETCOREAPP2_1_OR_GREATER
        private static readonly Func<string, string, StringComparison, bool> Contains = (s, value, comparison) => s.Contains(value, comparison);
#else
        private static readonly Func<string, string, StringComparison, bool> Contains = (s, value, comparison) => s.IndexOf(value, comparison) >= 0;
#endif

        private ConsoleColor foregroundColor = ConsoleColor.White;

        private ConsoleColor backgroundColor = ConsoleColor.Black;

        public ConcurrentQueue<string> Calls { get; } = new();

        public ConsoleColor ForegroundColor
        {
            get => this.foregroundColor;
            set
            {
                this.foregroundColor = value;
                this.Calls.Enqueue($"Foreground:{value}");
                Thread.Sleep(value == ConsoleColor.Yellow ? 200 : 5); // longer for warning
            }
        }

        public ConsoleColor BackgroundColor
        {
            get => this.backgroundColor;
            set
            {
                this.backgroundColor = value;
                this.Calls.Enqueue($"Background:{value}");
            }
        }

        /// <inheritdoc/>
        public object SyncRoot { get; } = new();

        public void ResetColor()
        {
            this.Calls.Enqueue("Reset");
        }

        public void Write(string value)
        {
            this.Calls.Enqueue($"Write:{value}");
            Thread.Sleep(Contains(value, "warn", StringComparison.OrdinalIgnoreCase) ? 300 : 5);
        }

        public void WriteLine(string value)
        {
            this.Calls.Enqueue($"WriteLine:{value}");
            Thread.Sleep(Contains(value, "warn", StringComparison.OrdinalIgnoreCase) ? 400 : 5);
        }
    }
}
