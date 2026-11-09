// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Conversions from <see cref="SeverityNumber"/>.
/// </summary>
internal static class SeverityNumberExtensions
{
    /// <summary>
    /// Converts a severity to the nearest <see cref="LogLevel"/>.
    /// </summary>
    /// <remarks>
    /// Each band of four severities collapses onto one level, because <see cref="LogLevel"/> has no
    /// finer steps.
    /// </remarks>
    /// <param name="severity">The severity to convert.</param>
    /// <returns>The matching level.</returns>
    internal static LogLevel ToLogLevel(this SeverityNumber severity) => severity switch
    {
        <= SeverityNumber.Trace4 => LogLevel.Trace,
        <= SeverityNumber.Debug4 => LogLevel.Debug,
        <= SeverityNumber.Info4 => LogLevel.Information,
        <= SeverityNumber.Warn4 => LogLevel.Warning,
        <= SeverityNumber.Error4 => LogLevel.Error,
        _ => LogLevel.Critical,
    };
}
