// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// A struct for accessing the <see cref="MetricPoint"/>s collected for a
/// <see cref="Metric"/>.
/// </summary>
public readonly struct MetricPointsAccessor
{
    // Holds either MetricPoint[] or SegmentedMetricPointStorage to preserve the public struct's layout.
    private readonly object metricPointStorage;
    private readonly int[] metricPointsToProcess;
    private readonly int targetCount;

    internal MetricPointsAccessor(MetricPoint[] metricsPoints, int[] metricPointsToProcess, int targetCount)
    {
        this.metricPointStorage = metricsPoints;
        this.metricPointsToProcess = metricPointsToProcess;
        this.targetCount = targetCount;
    }

    internal MetricPointsAccessor(SegmentedMetricPointStorage segmentedMetricPoints, int[] metricPointsToProcess, int targetCount)
    {
        this.metricPointStorage = segmentedMetricPoints;
        this.metricPointsToProcess = metricPointsToProcess;
        this.targetCount = targetCount;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="MetricPointsAccessor"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator()
        => new(this.metricPointStorage, this.metricPointsToProcess, this.targetCount);

#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    /// <summary>
    /// Enumerates the elements of a <see cref="MetricPointsAccessor"/>.
    /// </summary>
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        // Holds either MetricPoint[] or SegmentedMetricPointStorage to preserve the public struct's layout.
        private readonly object metricPointStorage;
        private readonly int[] metricPointsToProcess;
        private readonly int targetCount;
        private int index;

        internal Enumerator(object metricPointStorage, int[] metricPointsToProcess, int targetCount)
        {
            this.metricPointStorage = metricPointStorage;
            this.metricPointsToProcess = metricPointsToProcess;
            this.targetCount = targetCount;
            this.index = -1;
        }

        /// <summary>
        /// Gets the <see cref="MetricPoint"/> at the current position of the enumerator.
        /// </summary>
        public readonly ref readonly MetricPoint Current
        {
            get
            {
                var metricPointIndex = this.metricPointsToProcess[this.index];
                if (this.metricPointStorage is MetricPoint[] metricsPoints)
                {
                    return ref metricsPoints[metricPointIndex];
                }

                return ref ((SegmentedMetricPointStorage)this.metricPointStorage).GetMetricPoint(metricPointIndex);
            }
        }

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="MetricPointsAccessor"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
            => ++this.index < this.targetCount;
    }
}
