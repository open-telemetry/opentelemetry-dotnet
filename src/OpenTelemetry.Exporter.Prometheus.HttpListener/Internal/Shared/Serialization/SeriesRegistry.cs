// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// A position in a <see cref="SeriesRegistry"/> that it can be returned to.
/// </summary>
/// <param name="count">The number of series that had been added.</param>
/// <param name="droppedCount">The number of series that had been dropped.</param>
/// <param name="firstDroppedMetricName">The name of the first metric to have had a series dropped, if any.</param>
internal readonly struct SeriesMark(int count, int droppedCount, string? firstDroppedMetricName)
{
    public int Count { get; } = count;

    public int DroppedCount { get; } = droppedCount;

    public string? FirstDroppedMetricName { get; } = firstDroppedMetricName;
}

/// <summary>
/// Keeps track of the series (a metric name together with its labels) of the metrics of a metric family
/// that have been written to a scrape response, so that a series is never written twice.
/// </summary>
/// <remarks>
/// A series which appears more than once makes the whole response invalid, and it is then rejected in its entirety.
/// This can happen when tag keys are the same once they are escaped, or when metrics of different meters are in the
/// same family and the scope information that would tell them apart is disabled.
/// <para/>
/// A series is identified by the bytes that it is written as, so the labels of the series must be written in the same
/// order whenever they are the same. Series that are removed from the response must be removed using
/// <see cref="Rollback"/>, which also forgets the series that were dropped since the position was marked
/// with <see cref="Mark"/>. This type is not thread-safe.
/// </remarks>
internal sealed class SeriesRegistry
{
    private readonly HashSet<string> series = new(StringComparer.Ordinal);
    private readonly List<string> added = [];

    /// <summary>
    /// Gets the number of series of the family that have been added.
    /// </summary>
    public int Count => this.added.Count;

    /// <summary>
    /// Gets the number of series that were dropped because they were duplicates.
    /// </summary>
    public int DroppedCount { get; private set; }

    /// <summary>
    /// Gets the name of the first metric to have a series dropped because it was a duplicate, if any.
    /// </summary>
    public string? FirstDroppedMetricName { get; private set; }

    /// <summary>
    /// Removes everything, ready for a new response to be written.
    /// </summary>
    public void Clear()
    {
        this.StartFamily();

        this.DroppedCount = 0;
        this.FirstDroppedMetricName = null;
    }

    /// <summary>
    /// Removes the series, ready for the metrics of the next metric family to be written.
    /// </summary>
    public void StartFamily()
    {
        this.series.Clear();
        this.added.Clear();
    }

    /// <summary>
    /// Adds a series, unless it has already been added.
    /// </summary>
    /// <param name="buffer">The buffer containing the series.</param>
    /// <param name="start">The position in <paramref name="buffer"/> of the first byte of the series.</param>
    /// <param name="length">The length of the series in bytes.</param>
    /// <returns><see langword="true"/> if the series was added; <see langword="false"/> if it is a duplicate.</returns>
    public bool TryAdd(byte[] buffer, int start, int length)
    {
        var identity = Encoding.UTF8.GetString(buffer, start, length);

        if (!this.series.Add(identity))
        {
            return false;
        }

        this.added.Add(identity);

        return true;
    }

    /// <summary>
    /// Gets the current position, so that what is added or dropped after it can be removed with <see cref="Rollback"/>.
    /// </summary>
    /// <returns>The current position.</returns>
    public SeriesMark Mark() => new(this.added.Count, this.DroppedCount, this.FirstDroppedMetricName);

    /// <summary>
    /// Removes the series that were added after <paramref name="mark"/>, as they were removed from the response buffer,
    /// and forgets the series that were dropped after it, as the metric that dropped them is written again.
    /// </summary>
    /// <param name="mark">The position returned by <see cref="Mark"/> to return to.</param>
    public void Rollback(SeriesMark mark)
    {
        var count = mark.Count;

        for (var index = this.added.Count - 1; index >= count; index--)
        {
            _ = this.series.Remove(this.added[index]);
        }

        if (count < this.added.Count)
        {
            this.added.RemoveRange(count, this.added.Count - count);
        }

        this.DroppedCount = mark.DroppedCount;
        this.FirstDroppedMetricName = mark.FirstDroppedMetricName;
    }

    /// <summary>
    /// Records that a series of a metric was dropped as it was a duplicate.
    /// </summary>
    /// <param name="metricName">The name of the metric.</param>
    public void RecordDropped(string metricName)
    {
        this.DroppedCount++;
        this.FirstDroppedMetricName ??= metricName;
    }
}
