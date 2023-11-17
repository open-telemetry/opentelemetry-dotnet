// <copyright file="PrometheusSerializerExt.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

    public static int WriteMetric(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric)
    {
        cursor = WriteTypeMetadata(buffer, cursor, prometheusMetric);
        cursor = WriteUnitMetadata(buffer, cursor, prometheusMetric);
        cursor = WriteHelpMetadata(buffer, cursor, prometheusMetric, metric.Description);

        if (!metric.MetricType.IsHistogram())
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
                var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                // Counter and Gauge
                cursor = WriteMetricName(buffer, cursor, prometheusMetric);

                if (tags.Count > 0)
                {
                    buffer[cursor++] = unchecked((byte)'{');

                    foreach (var tag in tags)
                    {
                        cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
                        buffer[cursor++] = unchecked((byte)',');
                    }

                    buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
                }

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

                cursor = WriteLong(buffer, cursor, timestamp);

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

                    cursor = WriteMetricName(buffer, cursor, prometheusMetric);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_bucket{");

                    foreach (var tag in tags)
                    {
                        cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
                        buffer[cursor++] = unchecked((byte)',');
                    }

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

                    cursor = WriteLong(buffer, cursor, timestamp);

                    buffer[cursor++] = ASCII_LINEFEED;
                }

                // Histogram sum
                cursor = WriteMetricName(buffer, cursor, prometheusMetric);
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "_sum");

                if (tags.Count > 0)
                {
                    buffer[cursor++] = unchecked((byte)'{');

                    foreach (var tag in tags)
                    {
                        cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
                        buffer[cursor++] = unchecked((byte)',');
                    }

                    buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
                }

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());
                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteLong(buffer, cursor, timestamp);

                buffer[cursor++] = ASCII_LINEFEED;

                // Histogram count
                cursor = WriteMetricName(buffer, cursor, prometheusMetric);
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");

                if (tags.Count > 0)
                {
                    buffer[cursor++] = unchecked((byte)'{');

                    foreach (var tag in tags)
                    {
                        cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
                        buffer[cursor++] = unchecked((byte)',');
                    }

                    buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
                }

                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());
                buffer[cursor++] = unchecked((byte)' ');

                cursor = WriteLong(buffer, cursor, timestamp);

                buffer[cursor++] = ASCII_LINEFEED;
            }
        }

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }
}
