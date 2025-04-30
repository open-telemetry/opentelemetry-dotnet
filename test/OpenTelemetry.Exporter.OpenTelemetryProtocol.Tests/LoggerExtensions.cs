// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Hello from {Name} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, string name, double price);

    [LoggerMessage("Hello from {Name} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, LogLevel logLevel, string name, double price);

    [LoggerMessage(LogLevel.Information, EventId = 10, Message = "Hello from {Name} {Price}.")]
    public static partial void HelloFromWithEventId(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, EventId = 10, EventName = "MyEvent10", Message = "Hello from {Name} {Price}.")]
    public static partial void HelloFromWithEventIdAndEventName(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, "Log message")]
    public static partial void LogMessage(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Log when there is no activity.")]
    public static partial void LogWhenThereIsNoActivity(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Log within an activity.")]
    public static partial void LogWithinAnActivity(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "OpenTelemetry {Greeting} {Subject}!")]
    public static partial void OpenTelemetryGreeting(this ILogger logger, string greeting, string subject);

    [LoggerMessage(LogLevel.Information, "OpenTelemetry {AttributeOne} {AttributeTwo} {AttributeThree}!")]
    public static partial void OpenTelemetryWithAttributes(this ILogger logger, string attributeOne, string attributeTwo, string attributeThree);

    [LoggerMessage(LogLevel.Information, "Some log information message.")]
    public static partial void SomeLogInformation(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Hello from red-tomato")]
    public static partial void HelloFromRedTomato(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Hello from green-tomato")]
    public static partial void HelloFromGreenTomato(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Exception Occurred")]
    public static partial void ExceptionOccured(this ILogger logger, Exception exception);
}
