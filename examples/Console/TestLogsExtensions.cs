// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace Examples.Console;

internal static partial class TestLogsExtensions
{
    private static readonly Func<ILogger, string, IDisposable?> CityScope = LoggerMessage.DefineScope<string>("{City}");
    private static readonly Func<ILogger, string, IDisposable?> StoreTypeScope = LoggerMessage.DefineScope<string>("{StoreType}");

    public static IDisposable? BeginCityScope(this ILogger logger, string city) => CityScope(logger, city);

    public static IDisposable? BeginStoreTypeScope(this ILogger logger, string storeType) => StoreTypeScope(logger, storeType);

    [LoggerMessage(LogLevel.Information, "Hello from {Name} {Price}.")]
    public static partial void HelloFrom(this ILogger logger, string name, double price);
}
