// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// ExemplarReservoir base implementation and contract.
/// </summary>
/// <remarks>
/// <para experimental-warning="true"><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarreservoir"/>.
/// </remarks>
[Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
internal
#endif
    abstract class ExemplarReservoir
{
    // Note: This constructor is internal because we don't allow custom
    // ExemplarReservoir implementations to be based directly on the base class
    // only FixedSizeExemplarReservoir.
    internal ExemplarReservoir()
    {
    }

    /// <summary>
    /// Gets a value indicating whether or not the <see
    /// cref="ExemplarReservoir"/> should reset its state when performing
    /// collection.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="ResetOnCollect"/> is set to <see langword="true"/> for
    /// <see cref="MetricPoint"/>s using delta aggregation temporality and <see
    /// langword="false"/> for <see cref="MetricPoint"/>s using cumulative
    /// aggregation temporality.
    /// </remarks>
    public bool ResetOnCollect { get; private set; }

    /// <summary>
    /// Offers a measurement to the reservoir.
    /// </summary>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    public abstract void Offer(in ExemplarMeasurement<long> measurement);

    /// <summary>
    /// Offers a measurement to the reservoir.
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
