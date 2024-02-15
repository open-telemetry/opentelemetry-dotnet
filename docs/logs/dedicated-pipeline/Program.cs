// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DedicatedLogging;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Logging.AddOpenTelemetry(options =>
{
    // Set up primary pipeline for common app logs
    options.AddConsoleExporter();
});

builder.Services.AddDedicatedLogging(
    builder.Configuration.GetSection("DedicatedLogging"), // Bind configuration for dedicated logging pipeline
    options =>
    {
        // Set up secondary pipeline for dedicated logs
        options.AddConsoleExporter();
    });

var app = builder.Build();

app.MapGet("/", (HttpContext context, ILogger<Program> logger, IDedicatedLogger<Program> dedicatedLogger) =>
{
    // Standard log written
    logger.FoodPriceChanged("artichoke", 9.99);

    // Dedicated log written
    dedicatedLogger.RequestInitiated(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    return "Hello from OpenTelemetry Logs!";
});

app.Run();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, "Request initiated from `{ipAddress}`.")]
    public static partial void RequestInitiated(this IDedicatedLogger logger, string ipAddress);
}
