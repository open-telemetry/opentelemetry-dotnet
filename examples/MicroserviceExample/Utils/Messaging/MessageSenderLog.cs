// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Utils.Messaging;

internal static partial class MessageSenderLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Message sent: [{Body}]")]
    public static partial void MessageSent(this ILogger logger, string body);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Message publishing failed.")]
    public static partial void MessagePublishingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to inject trace context.")]
    public static partial void FailedToInjectTraceContext(this ILogger logger, Exception exception);
}
