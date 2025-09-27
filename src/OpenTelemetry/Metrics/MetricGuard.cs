// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Methods for guarding against exception throwing values in Metrics.
/// </summary>
internal partial class MetricGuard
{
    // Note: We don't use static readonly here because some customers
    // replace this using reflection which is not allowed on initonly static
    // fields. See: https://github.com/dotnet/runtime/issues/11571.
    // Customers: This is not guaranteed to work forever. We may change this
    // mechanism in the future do this at your own risk.
#if NET
    [GeneratedRegex(@"^[a-z][a-z0-9-._/]{0,254}$", RegexOptions.IgnoreCase)]
    public static partial Regex InstrumentNameRegex();
#else
    private static readonly Regex InstrumentNameRegexField = new(
        @"^[a-z][a-z0-9-._/]{0,254}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Regex InstrumentNameRegex() => InstrumentNameRegexField;
#endif

    /// <summary>
    /// Throws an exception if the given view name is invalid according to the specification.
    /// Null is valid because the instrument name will be used as the view name.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="viewName">The view name.</param>
    /// <param name="paramName">The parameter name to use in the thrown exception.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidViewName(string? viewName, [CallerArgumentExpression(nameof(viewName))] string? paramName = null)
    {
        if (!IsValidViewName(viewName))
        {
            throw new ArgumentException($"View name {viewName} is invalid.", paramName);
        }
    }

    /// <summary>
    /// Throws an exception if the given custom view name is invalid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="viewName">The view name.</param>
    /// <param name="paramName">The parameter name to use in the thrown exception.</param>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidCustomViewName(string? viewName, [CallerArgumentExpression(nameof(viewName))] string? paramName = null)
    {
        if (!IsValidInstrumentName(viewName))
        {
            throw new ArgumentException($"Custom view name {viewName} is invalid.", paramName);
        }
    }

    /// <summary>
    /// Returns whether the given instrument name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="instrumentName">The instrument name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidInstrumentName([NotNullWhen(true)] string? instrumentName)
    {
        if (string.IsNullOrWhiteSpace(instrumentName))
        {
            return false;
        }

        return InstrumentNameRegex().IsMatch(instrumentName);
    }

    /// <summary>
    /// Returns whether the given custom view name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="customViewName">The view name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    [DebuggerHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidViewName(string? customViewName)
    {
        // Only validate the view name in case it's not null. In case it's null, the view name will be the instrument name as per the spec.
        if (customViewName == null)
        {
            return true;
        }

        return InstrumentNameRegex().IsMatch(customViewName);
    }
}
