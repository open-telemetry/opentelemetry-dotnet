// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The base class for defining Exemplar Reservoir.
/// </summary>
internal abstract class ExemplarReservoir
{
    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    public abstract void Offer(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    public abstract void Offer(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    public abstract ReadOnlyExemplarCollection Collect();
}
