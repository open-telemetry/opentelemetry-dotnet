// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

var sdk = OpenTelemetrySdk.Create(builder => builder
    .WithLogging(logging => logging.AddConsoleExporter()));

var logger = sdk.GetLoggerFactory().CreateLogger<Program>();

var foodRecallNotice = new FoodRecallNotice
{
    BrandName = "Contoso",
    ProductDescription = "Salads",
    ProductType = "Food & Beverages",
    RecallReasonDescription = "due to a possible health risk from Listeria monocytogenes",
    CompanyName = "Contoso Fresh Vegetables, Inc.",
};

logger.FoodRecallNotice(foodRecallNotice);

// This will flush the remaining logs.
sdk.LoggerProvider.ForceFlush();

// Dispose SDK before the application ends.
sdk.Dispose();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Critical)]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] in FoodRecallNotice foodRecallNotice);
}
