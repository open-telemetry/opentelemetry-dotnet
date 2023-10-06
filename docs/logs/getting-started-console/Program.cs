// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

logger.FoodPriceChanged("artichoke", 9.99);

logger.FoodRecallNotice(
    logLevel: LogLevel.Critical,
    brandName: "Contoso",
    productDescription: "Salads",
    productType: "Food & Beverages",
    recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
    companyName: "Contoso Fresh Vegetables, Inc.");

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();

public static partial class ApplicationLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);

    [LoggerMessage(EventId = 2, Message = "A `{productType}` recall notice was published for `{brandName} {productDescription}` produced by `{companyName}` ({recallReasonDescription}).")]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        LogLevel logLevel,
        string brandName,
        string productDescription,
        string productType,
        string recallReasonDescription,
        string companyName);
}
