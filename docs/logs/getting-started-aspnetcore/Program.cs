// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// For instructional purposes only, disable the default .NET logging providers.
// We remove the console logging provider in this demo to use the verbose
// OpenTelemetry console exporter instead. For most development and production
// scenarios the default console provider works well and there is no need to
// clear these providers.
builder.Logging.ClearProviders();

// Add OpenTelemetry logging provider by calling the WithLogging extension.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
    .WithLogging(logging => logging
        /* Note: ConsoleExporter is used for demo purpose only. In production
           environment, ConsoleExporter should be replaced with other exporters
           (e.g. OTLP Exporter). */
        .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.FoodPriceChanged("artichoke", 9.99);

    return "Hello from OpenTelemetry Logs!";
});

app.Logger.StartingApp();

app.Run();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Starting the app...")]
    public static partial void StartingApp(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
