// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// A struct for accessing the <see cref="MetricPoint"/>s collected for a
/// <see cref="Metric"/>.
/// </summary>
public readonly struct MetricPointsAccessor : IEquatable<MetricPointsAccessor>
{
    private readonly MetricPoint[] metricsPoints;
    private readonly int[] metricPointsToProcess;
    private readonly int targetCount;

    internal MetricPointsAccessor(MetricPoint[] metricsPoints, int[] metricPointsToProcess, int targetCount)
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
    /// Compare two <see cref="MetricPointsAccessor"/> for equality.
    /// </summary>
    public static bool operator ==(MetricPointsAccessor left, MetricPointsAccessor right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="MetricPointsAccessor"/> for inequality.
    /// </summary>
    public static bool operator !=(MetricPointsAccessor left, MetricPointsAccessor right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MetricPointsAccessor other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(MetricPointsAccessor other)
        => ReferenceEquals(this.metricsPoints, other.metricsPoints)
        && ReferenceEquals(this.metricPointsToProcess, other.metricPointsToProcess)
        && this.targetCount == other.targetCount;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.metricsPoints, this.metricPointsToProcess, this.targetCount);
#else
        unchecked
        {
            var hash = 17;
            hash = (31 * hash) + (this.metricsPoints?.GetHashCode() ?? 0);
            hash = (31 * hash) + (this.metricPointsToProcess?.GetHashCode() ?? 0);
            hash = (31 * hash) + this.targetCount.GetHashCode();
            return hash;
        }
#endif
    }

#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    /// <summary>
    /// Enumerates the elements of a <see cref="MetricPointsAccessor"/>.
    /// </summary>
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        private readonly MetricPoint[] metricsPoints;
        private readonly int[] metricPointsToProcess;
        private readonly int targetCount;
        private int index;

        internal Enumerator(MetricPoint[] metricsPoints, int[] metricPointsToProcess, int targetCount)
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
