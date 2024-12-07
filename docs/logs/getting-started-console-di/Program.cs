// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

// Add services to the container, including OpenTelemetry with logging
var services = new ServiceCollection();
services.AddOpenTelemetry().WithLogging(logging => logging.AddConsoleExporter());
services.AddSingleton<ExampleService>();
ServiceProvider serviceProvider = services.BuildServiceProvider();

// Get the ExampleService object from the container
ExampleService service = serviceProvider.GetRequiredService<ExampleService>();

service.DoSomeWork();

// Dispose before the application ends.
serviceProvider.Dispose();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);

    [LoggerMessage(LogLevel.Critical, "A `{productType}` recall notice was published for `{brandName} {productDescription}` produced by `{companyName}` ({recallReasonDescription}).")]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        string brandName,
        string productDescription,
        string productType,
        string recallReasonDescription,
        string companyName);
}
