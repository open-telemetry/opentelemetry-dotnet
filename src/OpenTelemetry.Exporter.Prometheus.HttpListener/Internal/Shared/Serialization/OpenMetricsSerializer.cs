// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

    protected override bool EscapeHelpQuotationMarks => true;

    public override int WriteEof(byte[] buffer, int cursor)
    {
        // OpenMetrics expositions MUST be terminated with "# EOF".
        // See https://prometheus.io/docs/specs/om/open_metrics_spec/#overall-structure.
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# EOF");
        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    public override string GetMetadataName(PrometheusMetric metric)
        => metric.GetNameSet(this.Escaping).OpenMetricsMetadataName;

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
        => metric.GetNameSet(this.Escaping).OpenMetricsNameBytes;

    protected override ReadOnlySpan<byte> GetMetricMetadataNameBytes(PrometheusMetric metric)
        => metric.GetNameSet(this.Escaping).OpenMetricsMetadataNameBytes;

    protected override int WriteExplicitBound(byte[] buffer, int cursor, double explicitBound)
        => WriteCanonicalLabelValue(buffer, cursor, explicitBound);

    protected override bool ShouldWriteSumAndCount(bool hasNegativeBucketBounds)
        => !hasNegativeBucketBounds;

    protected override int WriteCounterExemplar(
        byte[] buffer,
        int cursor,
        in MetricPoint metricPoint,
        PrometheusMetric prometheusMetric,
        bool isLongValue)
    {
        if (prometheusMetric.Type == PrometheusType.Counter &&
            TryGetLatestExemplar(metricPoint, out var exemplar))
        {
            cursor = this.WriteExemplar(buffer, cursor, in exemplar, isLongValue);
        }

        return cursor;
    }

    protected override int WriteCounterCreated(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        in MetricPoint metricPoint,
        in TextFormatSerializerOptions options,
        ReadOnlySpan<byte> seriesAndTags)
    {
        if (prometheusMetric.Type != PrometheusType.Counter)
        {
            return cursor;
        }

        var startTime = metricPoint.StartTime;
        Debug.Assert(startTime != default, "Metric points must have a valid start time.");

        if (startTime == default)
        {
            return cursor;
        }

        // The series of a counter sample is its name followed by the braced tags, and the '_created'
        // series has the same tags, so copy them instead of serializing them a second time. A counter
        // that needs a quoted name starts with the brace, as the name is embedded within it.
        var tagsStart = seriesAndTags.IndexOf(unchecked((byte)'{'));

        if (tagsStart > 0)
        {
            cursor = this.WriteMetricNameWithSuffix(buffer, cursor, prometheusMetric, "_created");

            var tags = seriesAndTags.Slice(tagsStart);
            tags.CopyTo(new Span<byte>(buffer, cursor, tags.Length));
            cursor += tags.Length;
        }
        else
        {
            cursor = this.WriteSeriesAndTags(buffer, cursor, metric, prometheusMetric, metricPoint.Tags, options, "_created", reservedOutputKeys: null);
        }

        return WriteCreatedValue(buffer, cursor, startTime);
    }

    protected override int WriteHistogramBucketExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, double lowerBoundExclusive, double upperBoundInclusive)
    {
        if (TryGetLatestHistogramBucketExemplar(metricPoint, lowerBoundExclusive, upperBoundInclusive, out var exemplar))
        {
            cursor = this.WriteExemplar(buffer, cursor, in exemplar, isLongValue: false);
        }

        return cursor;
    }

    protected override int WriteHistogramCreated(byte[] buffer, int cursor, PrometheusMetric prometheusMetric, in MetricPoint metricPoint, ReadOnlySpan<byte> serializedTags)
    {
        var startTime = metricPoint.StartTime;
        Debug.Assert(startTime != default, "Metric points must have a valid start time.");

        if (startTime == default)
        {
            return cursor;
        }

        cursor = this.WriteSeriesNameAndSerializedTags(buffer, cursor, prometheusMetric, "_created", serializedTags);

        return WriteCreatedValue(buffer, cursor, startTime);
    }

    private static bool TryGetLatestExemplar(in MetricPoint metricPoint, out Exemplar exemplar)
    {
        exemplar = default;
        return metricPoint.TryGetExemplars(out var exemplars) && TryGetLatestExemplar(exemplars, out exemplar);
    }

    private static bool TryGetLatestExemplar(ReadOnlyExemplarCollection exemplars, out Exemplar exemplar)
    {
        exemplar = default;

        var found = false;

        foreach (ref readonly var candidate in exemplars)
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

        foreach (ref readonly var candidate in exemplars)
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

    private static int WriteCreatedValue(byte[] buffer, int cursor, DateTimeOffset startTime)
    {
        buffer[cursor++] = unchecked((byte)' ');

        cursor = WriteUnixTimeSeconds(buffer, cursor, startTime);

        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }
}
