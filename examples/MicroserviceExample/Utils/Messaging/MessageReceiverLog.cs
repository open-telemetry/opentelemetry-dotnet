// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Utils.Messaging;

internal static partial class MessageReceiverLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Message received: [{Message}]")]
    public static partial void MessageReceived(this ILogger logger, string message);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Message processing failed.")]
    public static partial void MessageProcessingFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to extract trace context.")]
    public static partial void FailedToExtractTraceContext(this ILogger logger, Exception exception);
}
