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
        bool enableOpenMetricsExemplarLabels,
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
                    cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue, openMetricsRequested, enableOpenMetricsExemplarLabels);
                }

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }
        else
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
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
                    cursor = WriteTags(buffer, cursor, metric, tags, openMetricsRequested, writeEnclosingBraces: false);

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

                    cursor = histogramMeasurement.ExplicitBound != double.PositiveInfinity
                        ? WriteDouble(buffer, cursor, histogramMeasurement.ExplicitBound)
                        : WriteAsciiStringNoEscape(buffer, cursor, "+Inf");

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "\"} ");

                    cursor = WriteLong(buffer, cursor, totalCount);

                    if (openMetricsRequested &&
                        TryGetLatestHistogramBucketExemplar(metricPoint, previousBound, histogramMeasurement.ExplicitBound, out var exemplar))
                    {
                        cursor = WriteExemplar(buffer, cursor, in exemplar, isLongValue: false, openMetricsRequested, enableOpenMetricsExemplarLabels);
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
                    cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags, openMetricsRequested);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());

                    buffer[cursor++] = ASCII_LINEFEED;

                    // Histogram count
                    cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");
                    cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags, openMetricsRequested);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());

                    buffer[cursor++] = ASCII_LINEFEED;
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
}
