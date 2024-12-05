// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

// Comment for PR to think about:
// -Unclear to me if this needs to be before LoggerFactory.Create and if they interact at all.
// -Unintituive that this `LoggerProvider` is not an `ILoggerProvider`.
// -In general, I think the `Sdk.Create*()` APIs are going to add confusion with the existing .Net blogs posts of
//  how to use ILogger, ILoggerProvider, and ILoggerFactory.
var loggerProvider = Sdk.CreateLoggerProviderBuilder().Build();

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
    brandName: "Contoso",
    productDescription: "Salads",
    productType: "Food & Beverages",
    recallReasonDescription: "due to a possible health risk from Listeria monocytogenes",
    companyName: "Contoso Fresh Vegetables, Inc.");

// This will flush the remaining logs.
loggerProvider.ForceFlush();

// This will shutdown the logging pipeline.
loggerFactory.Dispose();

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
