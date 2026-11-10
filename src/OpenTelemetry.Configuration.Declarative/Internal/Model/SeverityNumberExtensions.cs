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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="severity"/> is not a defined <see cref="SeverityNumber"/>.
    /// </exception>
    internal static LogLevel ToLogLevel(this SeverityNumber severity) => severity switch
    {
        >= SeverityNumber.Trace and <= SeverityNumber.Trace4 => LogLevel.Trace,
        >= SeverityNumber.Debug and <= SeverityNumber.Debug4 => LogLevel.Debug,
        >= SeverityNumber.Info and <= SeverityNumber.Info4 => LogLevel.Information,
        >= SeverityNumber.Warn and <= SeverityNumber.Warn4 => LogLevel.Warning,
        >= SeverityNumber.Error and <= SeverityNumber.Error4 => LogLevel.Error,
        >= SeverityNumber.Fatal and <= SeverityNumber.Fatal4 => LogLevel.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };
}
