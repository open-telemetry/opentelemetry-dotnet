// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddProcessor(new MyRedactionProcessor());
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

// Message will be redacted by MyRedactionProcessor
logger.FoodPriceChanged("<secret>", 9.99);

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();

public static partial class ApplicationLogs
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
