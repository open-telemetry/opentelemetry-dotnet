// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Serializes metrics using the OpenMetrics text format.
/// </summary>
internal abstract class OpenMetricsSerializer : TextFormatSerializer
{
    protected override string UnknownMetricTypeName => "unknown";

    protected override string TargetInfoTypeName => "target";

    protected override string TargetInfoTypeValue => "info";

    public override string GetMetadataName(PrometheusMetric metric)
        => metric.OpenMetricsMetadataName;

    internal static bool ShouldPreferExemplar(DateTimeOffset currentTimestamp, DateTimeOffset candidateTimestamp)
        => currentTimestamp <= candidateTimestamp;

    internal static bool IsHistogramBucketExemplarMatch(
        double exemplarValue,
        double lowerBoundExclusive,
        double upperBoundInclusive)
    {
        if (double.IsNaN(exemplarValue))
        {
            return false;
        }

        var isAboveLowerBound =
            exemplarValue > lowerBoundExclusive ||
            (lowerBoundExclusive == double.NegativeInfinity &&
             exemplarValue == double.NegativeInfinity);

        return exemplarValue <= upperBoundInclusive && isAboveLowerBound;
    }

    protected override ReadOnlySpan<byte> GetMetricNameBytes(PrometheusMetric metric)
        => metric.OpenMetricsNameBytes;

    protected override ReadOnlySpan<byte> GetMetricMetadataNameBytes(PrometheusMetric metric)
        => metric.OpenMetricsMetadataNameBytes;

    protected override int WriteExplicitBound(byte[] buffer, int cursor, double explicitBound)
        => WriteCanonicalLabelValue(buffer, cursor, explicitBound);

    protected override bool ShouldWriteSumAndCount(bool hasNegativeBucketBounds)
        => !hasNegativeBucketBounds;

    protected override int WriteCounterExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, PrometheusMetric prometheusMetric, bool isLongValue)
    {
        if (prometheusMetric.Type == PrometheusType.Counter &&
            TryGetLatestExemplar(metricPoint, out var exemplar))
        {
            cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue);
        }

        return cursor;
    }

    protected override int WriteCounterCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint)
    {
        if (prometheusMetric.Type == PrometheusType.Counter)
        {
            cursor = this.WriteCreatedMetric(buffer, cursor, metric, prometheusMetric, metricPoint);
        }

        return cursor;
    }

    protected override int WriteHistogramBucketExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, double lowerBoundExclusive, double upperBoundInclusive)
    {
        if (TryGetLatestHistogramBucketExemplar(metricPoint, lowerBoundExclusive, upperBoundInclusive, out var exemplar))
        {
            cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue: false);
        }

        return cursor;
    }

    protected override int WriteHistogramCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint)
        => this.WriteCreatedMetric(buffer, cursor, metric, prometheusMetric, metricPoint, ReservedHistogramLabelNames);

    private static bool TryGetLatestExemplar(in MetricPoint metricPoint, out Exemplar exemplar)
    {
        exemplar = default;
        return metricPoint.TryGetExemplars(out var exemplars) && TryGetLatestExemplar(exemplars, out exemplar);
    }

    private static bool TryGetLatestExemplar(ReadOnlyExemplarCollection exemplars, out Exemplar exemplar)
    {
        exemplar = default;

        var found = false;

        foreach (var candidate in exemplars)
        {
            if (!found || ShouldPreferExemplar(exemplar.Timestamp, candidate.Timestamp))
            {
                exemplar = candidate;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetLatestHistogramBucketExemplar(
        in MetricPoint metricPoint,
        double lowerBoundExclusive,
        double upperBoundInclusive,
        out Exemplar exemplar)
    {
        exemplar = default;
        return metricPoint.TryGetExemplars(out var exemplars) &&
               TryGetLatestHistogramBucketExemplar(exemplars, lowerBoundExclusive, upperBoundInclusive, out exemplar);
    }

    private static bool TryGetLatestHistogramBucketExemplar(
        ReadOnlyExemplarCollection exemplars,
        double lowerBoundExclusive,
        double upperBoundInclusive,
        out Exemplar exemplar)
    {
        exemplar = default;

        var found = false;

        foreach (var candidate in exemplars)
        {
            if (IsHistogramBucketExemplarMatch(candidate.DoubleValue, lowerBoundExclusive, upperBoundInclusive) &&
                (!found || ShouldPreferExemplar(exemplar.Timestamp, candidate.Timestamp)))
            {
                exemplar = candidate;
                found = true;
            }
        }

        return found;
    }

    private int WriteCreatedMetric(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        in MetricPoint metricPoint,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        if (metricPoint.StartTime == default)
        {
            return cursor;
        }

        cursor = this.WriteMetricMetadataName(buffer, cursor, prometheusMetric);

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "_created");
        cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags, reservedOutputKeys: reservedOutputKeys);

        buffer[cursor++] = unchecked((byte)' ');

        cursor = WriteUnixTimeSeconds(buffer, cursor, metricPoint.StartTime);

        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }
}
