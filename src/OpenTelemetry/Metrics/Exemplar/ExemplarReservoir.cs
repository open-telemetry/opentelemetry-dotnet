// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The base class for defining Exemplar Reservoir.
/// </summary>
internal abstract class ExemplarReservoir
{
    /// <summary>
    /// Gets a value indicating whether or not the <see
    /// cref="ExemplarReservoir"/> should reset its state when performing
    /// collection.
    /// </summary>
    public bool ResetOnCollect { get; private set; }

    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    public abstract void Offer(in ExemplarMeasurement<long> measurement);

    /// <summary>
    /// Offers measurement to the reservoir.
    /// </summary>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    public abstract void Offer(in ExemplarMeasurement<double> measurement);

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public abstract ReadOnlyExemplarCollection Collect();

    internal virtual void Initialize(AggregatorStore aggregatorStore)
    {
        this.ResetOnCollect = aggregatorStore.OutputDelta;
    }
}
