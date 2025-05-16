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

    public ReadOnlyExemplarCollection Exemplars = ReadOnlyExemplarCollection.Empty;

    private int isCriticalSectionOccupied;

    public MetricPointOptionalComponents Copy()
    {
        MetricPointOptionalComponents copy = new MetricPointOptionalComponents
        {
            HistogramBuckets = this.HistogramBuckets?.Copy(),
            Base2ExponentialBucketHistogram = this.Base2ExponentialBucketHistogram?.Copy(),
            Exemplars = this.Exemplars.Copy(),
        };

        return copy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AcquireLock()
    {
        if (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0)
        {
            this.AcquireLockRare();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseLock()
    {
        Volatile.Write(ref this.isCriticalSectionOccupied, 0);
    }

    // Note: This method is marked as NoInlining because the whole point of it
    // is to avoid the initialization of SpinWait unless it is needed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AcquireLockRare()
    {
        var sw = default(SpinWait);
        do
        {
            sw.SpinOnce();
        }
        while (Interlocked.Exchange(ref this.isCriticalSectionOccupied, 1) != 0);
    }
}
