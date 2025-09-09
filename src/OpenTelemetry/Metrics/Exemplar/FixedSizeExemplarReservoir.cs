// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// An <see cref="ExemplarReservoir"/> implementation which contains a fixed
/// number of <see cref="Exemplar"/>s.
/// </summary>
/// <remarks><inheritdoc cref="ExemplarReservoir" path="/remarks/para[@experimental-warning='true']"/></remarks>
[Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
internal
#endif
    abstract class FixedSizeExemplarReservoir : ExemplarReservoir
{
    private readonly Exemplar[] runningExemplars;
    private readonly Exemplar[] snapshotExemplars;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedSizeExemplarReservoir"/> class.
    /// </summary>
    /// <param name="capacity">The capacity (number of <see cref="Exemplar"/>s)
    /// to be contained in the reservoir.</param>
#pragma warning disable RS0022 // Constructor make noninheritable base class inheritable
    protected FixedSizeExemplarReservoir(int capacity)
#pragma warning restore RS0022 // Constructor make noninheritable base class inheritable
    {
        // Note: RS0022 is suppressed because we do want to allow custom
        // ExemplarReservoir implementations to be created by deriving from
        // FixedSizeExemplarReservoir.

        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.runningExemplars = new Exemplar[capacity];
        this.snapshotExemplars = new Exemplar[capacity];
        this.Capacity = capacity;
    }

    /// <summary>
    /// Gets the capacity (number of <see cref="Exemplar"/>s) contained in the
    /// reservoir.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public sealed override ReadOnlyExemplarCollection Collect()
    {
        var runningExemplars = this.runningExemplars;

        for (int i = 0; i < runningExemplars.Length; i++)
        {
            ref var running = ref runningExemplars[i];

            running.Collect(
                ref this.snapshotExemplars[i],
                reset: this.ResetOnCollect);
        }

        this.OnCollected();

        return new(this.snapshotExemplars);
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore)
    {
        var viewDefinedTagKeys = aggregatorStore.TagKeysInteresting;

        for (int i = 0; i < this.runningExemplars.Length; i++)
        {
            this.runningExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys;
            this.snapshotExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys;
        }

        base.Initialize(aggregatorStore);
    }

    internal void UpdateExemplar<T>(
        int exemplarIndex,
        in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        this.runningExemplars[exemplarIndex].Update(in measurement);
    }

    /// <summary>
    /// Fired when <see cref="Collect"/> has finished before returning a <see cref="ReadOnlyExemplarCollection"/>.
    /// </summary>
    /// <remarks>
    /// Note: This method is typically used to reset the state of reservoirs and
    /// is called regardless of the value of <see
    /// cref="ExemplarReservoir.ResetOnCollect"/>.
    /// </remarks>
    protected virtual void OnCollected()
    {
    }

    /// <summary>
    /// Updates the <see cref="Exemplar"/> stored in the reservoir at the
    /// specified index with an <see cref="ExemplarMeasurement{T}"/>.
    /// </summary>
    /// <param name="exemplarIndex">Index of the <see cref="Exemplar"/> to update.</param>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    protected void UpdateExemplar(
        int exemplarIndex,
        in ExemplarMeasurement<long> measurement)
    {
        this.runningExemplars[exemplarIndex].Update(in measurement);
    }

    /// <summary>
    /// Updates the <see cref="Exemplar"/> stored in the reservoir at the
    /// specified index with an <see cref="ExemplarMeasurement{T}"/>.
    /// </summary>
    /// <param name="exemplarIndex">Index of the <see cref="Exemplar"/> to update.</param>
    /// <param name="measurement"><see cref="ExemplarMeasurement{T}"/>.</param>
    protected void UpdateExemplar(
        int exemplarIndex,
        in ExemplarMeasurement<double> measurement)
    {
        this.runningExemplars[exemplarIndex].Update(in measurement);
    }
}
