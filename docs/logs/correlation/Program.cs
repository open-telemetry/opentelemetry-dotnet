// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

ActivitySource activitySource = new("MyCompany.MyProduct.MyLibrary");

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("MyCompany.MyProduct.MyLibrary")
    .AddConsoleExporter()
    .Build();

// Start an activity
using (var activity = activitySource.StartActivity("SayHello"))
{
    // Write a log within the context of an activity
    logger.FoodPriceChanged("artichoke", 9.99);
}

// Dispose tracer provider before the application ends.
// This will flush the remaining spans and shutdown the tracing pipeline.
tracerProvider.Dispose();

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();

public static partial class ApplicationLogs
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
