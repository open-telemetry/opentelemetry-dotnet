// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var loggerProvider = Sdk.CreateLoggerProviderBuilder().Build();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: "MyService",
            serviceVersion: "1.0.0"));
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

logger.FoodPriceChanged("artichoke", 9.99);

using (logger.BeginScope(new List<KeyValuePair<string, object>>
{
    new KeyValuePair<string, object>("store", "Seattle"),
}))
{
    logger.FoodPriceChanged("truffle", 999.99);
}

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
