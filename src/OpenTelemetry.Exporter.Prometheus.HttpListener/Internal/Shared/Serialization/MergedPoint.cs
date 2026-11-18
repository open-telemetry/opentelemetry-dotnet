// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// The values of the points of a metric that are written as the same series, added together.
/// </summary>
/// <remarks>
/// The series is written once, by the first of its points to be written, with these values. Anything else that
/// is written for the series, such as an exemplar or the time that it was created, is that of the first point.
/// </remarks>
/// <param name="identity">The identity of the series, as returned by <see cref="SeriesRegistry.GetIdentity"/>.</param>
internal sealed class MergedPoint(string identity)
{
    /// <summary>
    /// Gets the identity of the series.
    /// </summary>
    public string Identity { get; } = identity;

    /// <summary>
    /// Gets or sets a value indicating whether the series has been written, or dropped, already.
    /// </summary>
    public bool Written { get; set; }

    /// <summary>
    /// Gets or sets the sum of the values of the points of an integer sum.
    /// </summary>
    public long SumLong { get; set; }

    /// <summary>
    /// Gets or sets the sum of the values of the points of a floating-point sum.
    /// </summary>
    public double SumDouble { get; set; }

    /// <summary>
    /// Gets or sets the sum of the bucket counts of the points of a histogram, by bucket.
    /// </summary>
    public List<long>? BucketCounts { get; set; }

    /// <summary>
    /// Gets or sets the sum of the sums of the points of a histogram.
    /// </summary>
    public double HistogramSum { get; set; }

    /// <summary>
    /// Gets or sets the sum of the counts of the points of a histogram.
    /// </summary>
    public long HistogramCount { get; set; }
}
