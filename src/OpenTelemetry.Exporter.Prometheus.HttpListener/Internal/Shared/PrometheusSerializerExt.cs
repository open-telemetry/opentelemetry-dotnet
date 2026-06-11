// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// OpenTelemetry additions to the PrometheusSerializer.
/// </summary>
internal static partial class PrometheusSerializer
{
    public static bool CanWriteMetric(Metric metric)
    {
        if (metric.MetricType == MetricType.ExponentialHistogram)
        {
            // Exponential histograms are not yet support by Prometheus.
            // They are ignored for now.
            return false;
        }

        return true;
    }

    public static int WriteMetric(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        bool openMetricsRequested,
        bool writeType,
        bool writeUnit,
        bool writeHelp,
        string? unitOverride,
        string? helpOverride)
    {
        if (writeType)
        {
            cursor = WriteTypeMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        }

        if (writeUnit)
        {
            cursor = WriteUnitMetadata(buffer, cursor, prometheusMetric, unitOverride ?? prometheusMetric.Unit, openMetricsRequested);
        }

        if (writeHelp)
        {
            cursor = WriteHelpMetadata(buffer, cursor, prometheusMetric, helpOverride ?? metric.Description, openMetricsRequested);
        }

        if (!metric.MetricType.IsHistogram())
        {
            var isLongValue = ((int)metric.MetricType & 0b_0000_1111) == 0x0a; // I8

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                // Counter and Gauge
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags, openMetricsRequested);

                buffer[cursor++] = unchecked((byte)' ');

                if (isLongValue)
                {
                    cursor = metric.MetricType.IsSum()
                        ? WriteLong(buffer, cursor, metricPoint.GetSumLong())
                        : WriteLong(buffer, cursor, metricPoint.GetGaugeLastValueLong());
                }
                else
                {
                    cursor = metric.MetricType.IsSum()
                        ? WriteDouble(buffer, cursor, metricPoint.GetSumDouble())
                        : WriteDouble(buffer, cursor, metricPoint.GetGaugeLastValueDouble());
                }

                if (openMetricsRequested &&
                    prometheusMetric.Type == PrometheusType.Counter &&
                    TryGetLatestExemplar(metricPoint, out var exemplar))
                {
                    cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue, openMetricsRequested);
                }

                buffer[cursor++] = ASCII_LINEFEED;

                if (openMetricsRequested && prometheusMetric.Type == PrometheusType.Counter)
                {
                    cursor = WriteCreatedMetric(buffer, cursor, metric, prometheusMetric, metricPoint);
                }
            }
        }
        else
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
                var serializedTags = SerializeTags(metric, tags, openMetricsRequested, ReservedHistogramLabelNames);
                var hasNegativeBucketBounds = false;
                var previousBound = double.NegativeInfinity;

                long totalCount = 0;
                foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                {
                    if (openMetricsRequested && histogramMeasurement.ExplicitBound < 0)
                    {
                        hasNegativeBucketBounds = true;
                    }

                    totalCount += histogramMeasurement.BucketCount;

                    cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_bucket{");
                    cursor = WriteSerializedTagValues(buffer, cursor, serializedTags, appendTrailingComma: true);

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

                    if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                    {
                        cursor = openMetricsRequested
                            ? WriteCanonicalLabelValue(buffer, cursor, histogramMeasurement.ExplicitBound)
                            : WriteDouble(buffer, cursor, histogramMeasurement.ExplicitBound);
                    }
                    else
                    {
                        cursor = WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
                    }

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "\"} ");

                    cursor = WriteLong(buffer, cursor, totalCount);

                    if (openMetricsRequested &&
                        TryGetLatestHistogramBucketExemplar(metricPoint, previousBound, histogramMeasurement.ExplicitBound, out var exemplar))
                    {
                        cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue: false, openMetricsRequested);
                    }

                    buffer[cursor++] = ASCII_LINEFEED;
                    previousBound = histogramMeasurement.ExplicitBound;
                }

                if (!openMetricsRequested || !hasNegativeBucketBounds)
                {
                    // OpenMetrics histograms with negative bucket thresholds MUST NOT expose
                    // _sum and therefore MUST NOT expose _count.
                    // See https://prometheus.io/docs/specs/om/open_metrics_spec/#histogram-1
                    cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_sum");
                    cursor = WriteSerializedTags(buffer, cursor, serializedTags);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());

                    buffer[cursor++] = ASCII_LINEFEED;

                    // Histogram count
                    cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");
                    cursor = WriteSerializedTags(buffer, cursor, serializedTags);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());
                    buffer[cursor++] = ASCII_LINEFEED;
                }

                if (openMetricsRequested)
                {
                    cursor = WriteCreatedMetric(buffer, cursor, metric, prometheusMetric, metricPoint, ReservedHistogramLabelNames);
                }
            }
        }

        return cursor;
    }

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

    internal static bool TryGetLatestExemplar(ReadOnlyExemplarCollection exemplars, out Exemplar exemplar)
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

    internal static bool TryGetLatestHistogramBucketExemplar(
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

    internal static int GetNextSerializedTagsBufferSize(int currentBufferSize)
    {
        // Doubles the supplied buffer size, throwing once growth would exceed
        // MaxSerializedTagsBufferSize so that serializing an oversized tag set fails
        // fast instead of allocating without bound. An InvalidOperationException is
        // used deliberately: the buffer-growth retry loops in PrometheusCollectionManager
        // only retry on IndexOutOfRangeException/ArgumentException, so this terminates
        // the scrape immediately rather than repeatedly re-entering this allocation.
        var newBufferSize = currentBufferSize * 2;

        return newBufferSize <= 0 || newBufferSize > MaxSerializedTagsBufferSize
            ? throw new InvalidOperationException("The serialized Prometheus tag set exceeded the maximum supported size.")
            : newBufferSize;
    }

    private static bool TryGetLatestExemplar(in MetricPoint metricPoint, out Exemplar exemplar)
    {
        exemplar = default;
        return metricPoint.TryGetExemplars(out var exemplars) && TryGetLatestExemplar(exemplars, out exemplar);
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

    private static int WriteCreatedMetric(
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

        cursor = WriteMetricMetadataName(buffer, cursor, prometheusMetric, openMetricsRequested: true);

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "_created");
        cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags, openMetricsRequested: true, reservedOutputKeys: reservedOutputKeys);

        buffer[cursor++] = unchecked((byte)' ');

        cursor = WriteUnixTimeSeconds(buffer, cursor, metricPoint.StartTime);

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    private static byte[] SerializeTags(
        Metric metric,
        ReadOnlyTagCollection tags,
        bool openMetricsRequested,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        var buffer = new byte[128];

        while (true)
        {
            try
            {
                var cursor = WriteTags(
                    buffer,
                    0,
                    metric,
                    tags,
                    openMetricsRequested,
                    writeEnclosingBraces: false,
                    reservedOutputKeys: reservedOutputKeys);

                if (cursor > 0 && buffer[cursor - 1] == unchecked((byte)','))
                {
                    cursor--;
                }

                return buffer.AsSpan(0, cursor).ToArray();
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                buffer = new byte[GetNextSerializedTagsBufferSize(buffer.Length)];
            }
        }
    }

    private static int WriteSerializedTagValues(
        byte[] buffer,
        int cursor,
        ReadOnlySpan<byte> serializedTags,
        bool appendTrailingComma = false)
    {
        if (!serializedTags.IsEmpty)
        {
            if (serializedTags.Length > buffer.Length - cursor)
            {
                throw new ArgumentException("Destination buffer too small.", nameof(buffer));
            }

            serializedTags.CopyTo(buffer.AsSpan(cursor));
            cursor += serializedTags.Length;

            if (appendTrailingComma)
            {
                buffer[cursor++] = unchecked((byte)',');
            }
        }

        return cursor;
    }

    private static int WriteSerializedTags(
        byte[] buffer,
        int cursor,
        ReadOnlySpan<byte> serializedTags,
        bool appendTrailingComma = false)
    {
        buffer[cursor++] = unchecked((byte)'{');
        cursor = WriteSerializedTagValues(buffer, cursor, serializedTags, appendTrailingComma);

        buffer[cursor++] = unchecked((byte)'}');
        return cursor;
    }
}
