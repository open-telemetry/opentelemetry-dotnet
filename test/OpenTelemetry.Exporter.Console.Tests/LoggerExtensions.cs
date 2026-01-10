// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Exporter.Console.Tests;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Hello from {Name} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, "Message: {Message}")]
    public static partial void TestLog(this ILogger logger, string message);

    [LoggerMessage(LogLevel.Warning, "Message: {Message}")]
    public static partial void TestWarn(this ILogger logger, string message);

    [LoggerMessage(LogLevel.Error, "Message: {Message}")]
    public static partial void TestError(this ILogger logger, string message);

    [LoggerMessage(LogLevel.Critical, "This is a critical error with exception")]
    public static partial void ExceptionTest(this ILogger logger, Exception ex);
}
