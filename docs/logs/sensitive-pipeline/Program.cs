// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;
using SensitiveLogging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Logging.AddOpenTelemetry(options =>
{
    // Set up primary pipeline for common non-PII logs
    options.AddConsoleExporter();
});

builder.Services.AddPiiLogging(
    builder.Configuration.GetSection("SensitiveLogging"), // Bind configuration for sensitive logging pipeline
    options =>
    {
        // Set up secondary pipeline for PII logs
        options.AddConsoleExporter();
    });

var app = builder.Build();

app.MapGet("/", (HttpContext context, ILogger<Program> logger, IPiiLogger<Program> piiLogger) =>
{
    // Standard non-sensitive log written
    logger.FoodPriceChanged("artichoke", 9.99);

    // Sensitive log written
    piiLogger.RequestInitiated(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    return "Hello from OpenTelemetry Logs!";
});

app.Run();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Information, "Request initiated from `{ipAddress}`.")]
    public static partial void RequestInitiated(this IPiiLogger logger, string ipAddress);
}
