// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Benchmarks.Logs;

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Critical, "A `{productType}` recall notice was published for `{brandName} {productDescription}` produced by `{companyName}` ({recallReasonDescription}).")]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        string brandName,
        string productDescription,
        string productType,
        string recallReasonDescription,
        string companyName);

    [LoggerMessage(LogLevel.Information, "Hello from {Food} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, string food, double price);
}
