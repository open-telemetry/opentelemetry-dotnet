// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

var sdk = OpenTelemetrySdk.Create(builder => builder
    .WithLogging(logging => logging.AddConsoleExporter()));

var logger = sdk.GetLoggerFactory().CreateLogger<Program>();

// Message will be redacted by MyRedactionProcessor
logger.FoodPriceChanged("<secret>", 9.99);

// Dispose SDK before the application ends.
sdk.Dispose();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
