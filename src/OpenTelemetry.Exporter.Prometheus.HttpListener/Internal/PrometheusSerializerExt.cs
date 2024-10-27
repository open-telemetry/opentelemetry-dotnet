// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

    public static int WriteMetric(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, bool openMetricsRequested = false)
    {
        cursor = WriteTypeMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteUnitMetadata(buffer, cursor, prometheusMetric, openMetricsRequested);
        cursor = WriteHelpMetadata(buffer, cursor, prometheusMetric, metric.Description, openMetricsRequested);

        var isLong = metric.MetricType.IsLong();
        if (!metric.MetricType.IsHistogram())
        {
            var isSum = metric.MetricType.IsSum();

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                // Counter and Gauge
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                if (isLong)
                {
                    if (isSum)
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
                    if (isSum)
                    {
                        cursor = WriteDouble(buffer, cursor, metricPoint.GetSumDouble());
                    }
                    else
                    {
                        cursor = WriteDouble(buffer, cursor, metricPoint.GetGaugeLastValueDouble());
                    }
                }

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);

                if (isSum && openMetricsRequested && metricPoint.TryGetExemplars(out var exemplarCollection))
                {
                    cursor = WriteSumExemplar(buffer, cursor, metric, exemplarCollection);
                }

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }
        else
        {
            Debug.Assert(!isLong, "Expected histogram metric to be of type `double`");

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                metricPoint.TryGetExemplars(out var exemplarCollection);
                var exemplars = exemplarCollection.GetEnumerator();
                var hasExemplar = exemplars.MoveNext();

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

                    cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);

                    if (hasExemplar && openMetricsRequested)
                    {
                        if (exemplars.Current.DoubleValue <= histogramMeasurement.ExplicitBound)
                        {
                            cursor = WriteExemplar(buffer, cursor, exemplars.Current, metric.Name, isLong: false);
                        }

                        while (hasExemplar && exemplars.Current.DoubleValue <= histogramMeasurement.ExplicitBound)
                        {
                            hasExemplar = exemplars.MoveNext();
                        }
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

                cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);

                buffer[cursor++] = ASCII_LINEFEED;

                // Histogram count
                cursor = WriteMetricName(buffer, cursor, prometheusMetric, openMetricsRequested);
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");
                cursor = WriteTags(buffer, cursor, metric, metricPoint.Tags);

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());
                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteTimestamp(buffer, cursor, timestamp, openMetricsRequested);

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }

        return cursor;
    }

    private static int WriteSumExemplar(
        byte[] buffer,
        int cursor,
        in Metric metric,
        in ReadOnlyExemplarCollection exemplarCollection)
    {
        var exemplars = exemplarCollection.GetEnumerator();
        if (!exemplars.MoveNext())
        {
            return cursor;
        }

        ref readonly Exemplar maxExemplar = ref exemplars.Current;
        var isLong = metric.MetricType.IsLong();

        while (exemplars.MoveNext())
        {
            if (isLong)
            {
                if (exemplars.Current.LongValue >= maxExemplar.LongValue)
                {
                    maxExemplar = ref exemplars.Current;
                }
            }
            else
            {
                if (exemplars.Current.DoubleValue >= maxExemplar.DoubleValue)
                {
                    maxExemplar = ref exemplars.Current;
                }
            }
        }

        return WriteExemplar(buffer, cursor, maxExemplar, metric.Name, isLong);
    }

    private static int WriteExemplar(byte[] buffer, int cursor, in Exemplar exemplar, string metricName, bool isLong)
    {
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'#');
        buffer[cursor++] = unchecked((byte)' ');

        buffer[cursor++] = unchecked((byte)'{');
        var labelSetCursorStart = cursor;
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "trace_id=\"");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, exemplar.TraceId.ToHexString());
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "\",span_id=\"");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, exemplar.SpanId.ToHexString());
        buffer[cursor++] = unchecked((byte)'"');
        buffer[cursor++] = unchecked((byte)',');

        var labelSetWritten = cursor - labelSetCursorStart - 8;

        var tagResetCursor = cursor;

        foreach (var tag in exemplar.FilteredTags)
        {
            var prevCursor = cursor;
            cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);

            // From the spec:
            //   Other characters in the text rendering of an exemplar such as ",= are not included in this limit
            //   for implementation simplicity and for consistency between the text and proto formats.
            labelSetWritten += cursor - prevCursor - 3; // subtract 2 x " and 1 x = character

            buffer[cursor++] = unchecked((byte)',');

            // From the spec:
            //   The combined length of the label names and values of an Exemplar's LabelSet MUST NOT exceed 128 UTF-8 character code points.
            if (labelSetWritten > 128)
            {
                cursor = tagResetCursor;
                PrometheusExporterEventSource.Log.ExemplarTagsTooLong(metricName);
                break;
            }
        }

        buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
        buffer[cursor++] = unchecked((byte)' ');

        if (isLong)
        {
            cursor = WriteLong(buffer, cursor, exemplar.LongValue);
        }
        else
        {
            cursor = WriteDouble(buffer, cursor, exemplar.DoubleValue);
        }

        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteTimestamp(buffer, cursor, exemplar.Timestamp.ToUnixTimeMilliseconds(), useOpenMetrics: true);

        return cursor;
    }
}
