// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace ExtendingTheSdk;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Hello from {Name} {Price}")]
    public static partial void HelloFrom(this ILogger logger, string name, double price);
}
