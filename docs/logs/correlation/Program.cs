// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

ActivitySource activitySource = new("MyCompany.MyProduct.MyLibrary");

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("MyCompany.MyProduct.MyLibrary")
    .AddConsoleExporter()
    .Build();

// Start an activity
using (var activity = activitySource.StartActivity("SayHello"))
{
    // Write a log within the context of an activity
    logger.FoodPriceChanged("artichoke", 9.99);
}

public static partial class ApplicationLogs
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
