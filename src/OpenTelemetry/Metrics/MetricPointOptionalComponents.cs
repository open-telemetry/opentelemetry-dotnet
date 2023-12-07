// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores optional components of a metric point.
/// Histogram, Exemplar are current components.
/// ExponentialHistogram is a future component.
/// This is done to keep the MetricPoint (struct)
/// size in control.
/// </summary>
internal sealed class MetricPointOptionalComponents
{
    public HistogramBuckets? HistogramBuckets;

    public Base2ExponentialBucketHistogram? Base2ExponentialBucketHistogram;

    public ExemplarReservoir? ExemplarReservoir;

    public Exemplar[]? Exemplars;

    private int isCriticalSectionOccupied = 0;

    internal MetricPointOptionalComponents Copy()
    {
        MetricPointOptionalComponents copy = new MetricPointOptionalComponents
        {
            HistogramBuckets = this.HistogramBuckets?.Copy(),
            Base2ExponentialBucketHistogram = this.Base2ExponentialBucketHistogram?.Copy(),
        };

        if (this.Exemplars != null)
        {
            copy.Exemplars = new Exemplar[this.Exemplars.Length];
            Array.Copy(this.Exemplars, copy.Exemplars, this.Exemplars.Length);
        }

        return copy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AcquireLock()
    {
        var sw = default(SpinWait);
        while (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            sw.SpinOnce();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseLock()
    {
        Interlocked.Exchange(ref this.isCriticalSectionOccupied, 0);
    }
}
