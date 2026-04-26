// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
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

    public static int WriteMetric(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, bool openMetricsRequested, bool disableTimestamp)
    {
        cursor = WriteTypeMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteUnitMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteHelpMetadata(buffer, cursor, prometheusMetric, metric.Description, openMetricsRequested);

        var metricType = metric.MetricType;
        if (!metricType.IsHistogram())
        {
            var isLongValue = ((int)metricType & 0b_0000_1111) == 0x0a; // I8
            var isSum = metricType.IsSum();

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                // Counter and Gauge
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteTags(buffer, cursor, prometheusMetric, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                if (isLongValue)
                {
                    long value = isSum ? metricPoint.GetSumLong() : metricPoint.GetGaugeLastValueLong();
                    cursor = WriteLong(buffer, cursor, value);
                }
                else
                {
                    double value = isSum ? metricPoint.GetSumDouble() : metricPoint.GetGaugeLastValueDouble();
                    cursor = WriteDouble(buffer, cursor, value);
                }

                if (!disableTimestamp)
                {
                    buffer[cursor++] = unchecked((byte)' ');
                    cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
                }

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }
        else
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                cursor = WriteHistogramMetricPoint(buffer, cursor, metric, prometheusMetric, in metricPoint, openMetricsRequested, disableTimestamp);
            }
        }

        return cursor;
    }

    internal static byte[] SerializeStaticTags(Metric metric)
    {
#if NET
        Span<byte> stackBuffer = stackalloc byte[256];
        if (TryWriteStaticTags(stackBuffer, metric, out var stackCursor))
        {
            return stackBuffer[..stackCursor].ToArray();
        }
#endif

        var buffer = new byte[128];

        while (true)
        {
            try
            {
                var cursor = WriteStaticTags(buffer, 0, metric);
                return buffer.AsSpan(0, cursor).ToArray();
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                buffer = new byte[checked(buffer.Length * 2)];
            }
        }
    }

    private static int WriteHistogramMetricPoint(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        in MetricPoint metricPoint,
        bool openMetricsRequested,
        bool disableTimestamp)
    {
#if NET
        Span<byte> stackTags = stackalloc byte[256];
        if (TryWriteTags(stackTags, prometheusMetric, metric, metricPoint.Tags, writeEnclosingBraces: false, out var stackTagsLength))
        {
            return WriteHistogramMetricPoint(buffer, cursor, prometheusMetric, in metricPoint, openMetricsRequested, disableTimestamp, stackTags[..stackTagsLength]);
        }
#endif

        var serializedTags = RentSerializedTags(prometheusMetric, metric, metricPoint.Tags, out var tagsLength);

        try
        {
            return WriteHistogramMetricPoint(buffer, cursor, prometheusMetric, in metricPoint, openMetricsRequested, disableTimestamp, serializedTags.AsSpan(0, tagsLength));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(serializedTags);
        }
    }

    private static int WriteHistogramMetricPoint(
        byte[] buffer,
        int cursor,
        PrometheusMetric prometheusMetric,
        in MetricPoint metricPoint,
        bool openMetricsRequested,
        bool disableTimestamp,
        ReadOnlySpan<byte> tags)
    {
        var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

        long totalCount = 0;
        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
        {
            totalCount += histogramMeasurement.BucketCount;

            cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
            cursor = WriteAsciiStringNoEscape(buffer, cursor, "_bucket{");
            cursor = WriteUtf8NoEscape(buffer, cursor, tags);

            cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

            cursor = histogramMeasurement.ExplicitBound != double.PositiveInfinity
                ? WriteDouble(buffer, cursor, histogramMeasurement.ExplicitBound)
                : WriteAsciiStringNoEscape(buffer, cursor, "+Inf");

            cursor = WriteAsciiStringNoEscape(buffer, cursor, "\"} ");

            cursor = WriteLong(buffer, cursor, totalCount);

            if (!disableTimestamp)
            {
                buffer[cursor++] = unchecked((byte)' ');
                cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
            }

            buffer[cursor++] = ASCII_LINEFEED;
        }

        // Histogram sum
        cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "_sum{");
        cursor = WriteUtf8NoEscape(buffer, cursor, tags);
        buffer[cursor - 1] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());

        if (!disableTimestamp)
        {
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
        }

        buffer[cursor++] = ASCII_LINEFEED;

        // Histogram count
        cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count{");
        cursor = WriteUtf8NoEscape(buffer, cursor, tags);
        buffer[cursor - 1] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());

        if (!disableTimestamp)
        {
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);
        }

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    private static byte[] RentSerializedTags(PrometheusMetric prometheusMetric, Metric metric, ReadOnlyTagCollection tags, out int tagsLength)
    {
        var length = Math.Max(prometheusMetric.SerializedStaticTags?.Length ?? 0, 64) + 128;
        var buffer = ArrayPool<byte>.Shared.Rent(length);

        while (true)
        {
            try
            {
                tagsLength = WriteTags(buffer, 0, prometheusMetric, metric, tags, writeEnclosingBraces: false);
                return buffer;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = ArrayPool<byte>.Shared.Rent(checked(buffer.Length * 2));
            }
        }
    }
}
