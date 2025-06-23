// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using ComplexObjects;
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

var foodRecallNotice = new FoodRecallNotice
{
    BrandName = "Contoso",
    ProductDescription = "Salads",
    ProductType = "Food & Beverages",
    RecallReasonDescription = "due to a possible health risk from Listeria monocytogenes",
    CompanyName = "Contoso Fresh Vegetables, Inc.",
};

logger.FoodRecallNotice(foodRecallNotice);

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Critical)]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] in FoodRecallNotice foodRecallNotice);
}
