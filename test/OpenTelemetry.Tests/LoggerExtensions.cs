// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Tests;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Hello world {Data}")]
    public static partial void HelloWorld(this ILogger logger, int data);

    [LoggerMessage(LogLevel.Information, "Hello world {Data}")]
    public static partial void HelloWorld(this ILogger logger, string data);

    [LoggerMessage(LogLevel.Information, "Hello, World!")]
    public static partial void HelloWorld(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Hello from {Name} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, "{Food}")]
    public static partial void Food(this ILogger logger, object food);

    [LoggerMessage(LogLevel.Information, "Log")]
    public static partial void Log(this ILogger logger);

    [LoggerMessage("Log")]
    public static partial void Log(this ILogger logger, LogLevel logLevel);

    [LoggerMessage(LogLevel.Information, "Log within a dropped activity")]
    public static partial void LogWithinADroppedActivity(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Log within activity marked as RecordOnly")]
    public static partial void LogWithinRecordOnlyActivity(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Log within activity marked as RecordAndSample")]
    public static partial void LogWithinRecordAndSampleActivity(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Dispose called")]
    public static partial void DisposedCalled(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "{Product} {Year}!")]
    public static partial void LogProduct(this ILogger logger, string product, int year);

    [LoggerMessage(LogLevel.Information, "{Product} {Year} {Complex}!")]
    public static partial void LogProduct(this ILogger logger, string product, int year, object complex);

    [LoggerMessage(LogLevel.Information, "Exception Occurred")]
    public static partial void LogException(this ILogger logger, Exception exception);
}
