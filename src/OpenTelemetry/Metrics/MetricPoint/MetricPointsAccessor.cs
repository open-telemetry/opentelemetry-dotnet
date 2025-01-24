// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A struct for accessing the <see cref="MetricPoint"/>s collected for a
/// <see cref="Metric"/>.
/// </summary>
public readonly struct MetricPointsAccessor
{
    private readonly MetricPoint[] metricsPoints;
    private readonly int[] metricPointsToProcess;
    private readonly long targetCount;

    internal MetricPointsAccessor(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount)
    {
        this.metricsPoints = metricsPoints;
        this.metricPointsToProcess = metricPointsToProcess;
        this.targetCount = targetCount;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="MetricPointsAccessor"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator()
        => new(this.metricsPoints, this.metricPointsToProcess, this.targetCount);

    /// <summary>
    /// Enumerates the elements of a <see cref="MetricPointsAccessor"/>.
    /// </summary>
    public struct Enumerator
    {
        private readonly MetricPoint[] metricsPoints;
        private readonly int[] metricPointsToProcess;
        private readonly long targetCount;
        private long index;

        internal Enumerator(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount)
        {
            this.metricsPoints = metricsPoints;
            this.metricPointsToProcess = metricPointsToProcess;
            this.targetCount = targetCount;
            this.index = -1;
        }

        /// <summary>
        /// Gets the <see cref="MetricPoint"/> at the current position of the enumerator.
        /// </summary>
        public readonly ref readonly MetricPoint Current
            => ref this.metricsPoints[this.metricPointsToProcess[this.index]];

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
