// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Mechanical (non-subjective) mapping from a closed generic instrument Type
/// to an  <see cref="InstrumentKind"/> + <see cref="NumericKind"/> pair.
/// Kept separate from  <see cref="AggregationCompatibility"/> so a
/// reviewer's attention is  drawn to the actual policy table, not to
/// this reflection plumbing.
/// </summary>
internal static class InstrumentTypeInspector
{
    /// <summary>
    /// Attempts to classify <paramref name="instrumentType"/> into an
    /// <see cref="InstrumentKind"/> and <see cref="NumericKind"/>.
    /// </summary>
    /// <param name="instrumentType">The closed generic instrument type.</param>
    /// <param name="kind">The classified instrument kind,
    /// if successful.</param>
    /// <param name="numeric">The classified numeric kind,
    /// if successful.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="instrumentType"/> is
    /// a recognized
    /// System.Diagnostics.Metrics instrument type; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryClassify(Type instrumentType, out InstrumentKind kind)
    {
        ArgumentNullException.ThrowIfNull(instrumentType);

        kind = default;

        if (!instrumentType.IsGenericType)
        {
            return false;
        }

        var genericDefinition = instrumentType.GetGenericTypeDefinition();

        if (genericDefinition == typeof(Counter<>))
        {
            kind = InstrumentKind.Counter;
        }
        else if (genericDefinition == typeof(UpDownCounter<>))
        {
            kind = InstrumentKind.UpDownCounter;
        }
        else if (genericDefinition == typeof(Histogram<>))
        {
            kind = InstrumentKind.Histogram;
        }
        else if (genericDefinition == typeof(Gauge<>))
        {
            kind = InstrumentKind.Gauge;
        }
        else if (genericDefinition == typeof(ObservableCounter<>))
        {
            kind = InstrumentKind.ObservableCounter;
        }
        else if (genericDefinition == typeof(ObservableUpDownCounter<>))
        {
            kind = InstrumentKind.ObservableUpDownCounter;
        }
        else if (genericDefinition == typeof(ObservableGauge<>))
        {
            kind = InstrumentKind.ObservableGauge;
        }
        else
        {
            return false;
        }

        var valueType = instrumentType.GetGenericArguments()[0];

        return true;
    }
}