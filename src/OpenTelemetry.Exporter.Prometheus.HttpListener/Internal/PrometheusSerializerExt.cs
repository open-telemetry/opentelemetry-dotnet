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

    public static int WriteMetric(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, bool openMetricsRequested = false, bool disableTimestamp = false)
    {
        cursor = WriteTypeMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteUnitMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteHelpMetadata(buffer, cursor, prometheusMetric, metric.Description, openMetricsRequested);

        if (!metric.MetricType.IsHistogram())
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                // Counter and Gauge
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                // TODO: MetricType is same for all MetricPoints
                // within a given Metric, so this check can avoided
                // for each MetricPoint
                if (((int)metric.MetricType & 0b_0000_1111) == 0x0a /* I8 */)
                {
                    if (metric.MetricType.IsSum())
                    {
                        cursor = WriteLong(buffer, cursor, metricPoint.GetSumLong());
                    }
                    else
                    {
                        cursor = WriteLong(buffer, cursor, metricPoint.GetGaugeLastValueLong());
                    }
                }
                else
                {
                    if (metric.MetricType.IsSum())
                    {
                        cursor = WriteDouble(buffer, cursor, metricPoint.GetSumDouble());
                    }
                    else
                    {
                        cursor = WriteDouble(buffer, cursor, metricPoint.GetGaugeLastValueDouble());
                    }
                }

                buffer[cursor++] = unchecked((byte)' ');

                if (!disableTimestamp)
                {
                    cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
                }

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }
        else
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                long totalCount = 0;
                foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                {
                    totalCount += histogramMeasurement.BucketCount;

                    cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_bucket{");
                    cursor = WriteTags(buffer, cursor, metric, tags, writeEnclosingBraces: false);

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

                    if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                    {
                        cursor = WriteDouble(buffer, cursor, histogramMeasurement.ExplicitBound);
                    }
                    else
                    {
                        cursor = WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
                    }

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "\"} ");

                    cursor = WriteLong(buffer, cursor, totalCount);
                    buffer[cursor++] = unchecked((byte)' ');

                    if (!disableTimestamp)
                    {
                        cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
                    }

                    buffer[cursor++] = ASCII_LINEFEED;
                }

                // Histogram sum
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "_sum");
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());
                buffer[cursor++] = unchecked((byte)' ');

                if (!disableTimestamp)
                {
                    cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
                }

                buffer[cursor++] = ASCII_LINEFEED;

                // Histogram count
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());
                buffer[cursor++] = unchecked((byte)' ');

                if (!disableTimestamp)
                {
                    cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
                }

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }

        return cursor;
    }
}
