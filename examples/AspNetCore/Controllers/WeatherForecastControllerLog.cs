// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Examples.AspNetCore.Controllers;

internal static partial class WeatherForecastControllerLog
{
    private static readonly Func<ILogger, string, IDisposable?> Scope = LoggerMessage.DefineScope<string>("{Id}");

    public static IDisposable? BeginIdScope(this ILogger logger, string id) => Scope(logger, id);

    [LoggerMessage(EventId = 1, Message = "WeatherForecasts generated {Count}: {Forecasts}")]
    public static partial void WeatherForecastGenerated(this ILogger logger, LogLevel logLevel, int count, WeatherForecast[] forecasts);
}
