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

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// OpenTelemetry additions to the PrometheusSerializer.
    /// </summary>
    internal static partial class PrometheusSerializer
    {
        private static readonly string[] MetricTypes = new string[] { "untyped", "counter", "gauge", "histogram", "summary" };

        public static int WriteMetric(byte[] buffer, int cursor, Metric metric)
        {
            if (!string.IsNullOrWhiteSpace(metric.Description))
            {
                cursor = WriteHelpText(buffer, cursor, metric.Name, metric.Unit, metric.Description);
            }

            int metricType = (int)metric.MetricType >> 4;
            cursor = WriteTypeInfo(buffer, cursor, metric.Name, metric.Unit, MetricTypes[metricType]);

            if (metric.MetricType != MetricType.Histogram)
            {
                foreach (ref var metricPoint in metric.GetMetricPoints())
                {
                    var keys = metricPoint.Keys;
                    var values = metricPoint.Values;
                    var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                    // Counter and Gauge
                    cursor = WriteMetricName(buffer, cursor, metric.Name, metric.Unit);

                    if (keys != null && keys.Length > 0)
                    {
                        buffer[cursor++] = unchecked((byte)'{');

                        for (var i = 0; i < keys.Length; i++)
                        {
                            if (i > 0)
                            {
                                buffer[cursor++] = unchecked((byte)',');
                            }

                            cursor = WriteLabel(buffer, cursor, keys[i], values[i]);
                        }

                        buffer[cursor++] = unchecked((byte)'}');
                    }

                    buffer[cursor++] = unchecked((byte)' ');

                    if (((int)metric.MetricType & 0b_0000_1111) == 0x0a /* I8 */)
                    {
                        cursor = WriteLong(buffer, cursor, metricPoint.LongValue);
                    }
                    else
                    {
                        cursor = WriteDouble(buffer, cursor, metricPoint.DoubleValue);
                    }

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, timestamp);

                    buffer[cursor++] = ASCII_LINEFEED;
                }
            }
            else
            {
                foreach (ref var metricPoint in metric.GetMetricPoints())
                {
                    var keys = metricPoint.Keys;
                    var values = metricPoint.Values;
                    var timestamp = metricPoint.EndTime.ToUnixTimeMilliseconds();

                    // Histogram buckets
                    var bucketCounts = metricPoint.BucketCounts;
                    var explicitBounds = metricPoint.ExplicitBounds;
                    long totalCount = 0;
                    for (int idxBound = 0; idxBound < explicitBounds.Length + 1; idxBound++)
                    {
                        totalCount += bucketCounts[idxBound];

                        cursor = WriteMetricName(buffer, cursor, metric.Name, metric.Unit);
                        cursor = WriteAsciiStringNoEscape(buffer, cursor, "_bucket{");

                        if (keys != null)
                        {
                            for (var i = 0; i < keys.Length; i++)
                            {
                                cursor = WriteLabel(buffer, cursor, keys[i], values[i]);
                                buffer[cursor++] = unchecked((byte)',');
                            }
                        }

                        cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

                        if (idxBound < explicitBounds.Length)
                        {
                            cursor = WriteDouble(buffer, cursor, explicitBounds[idxBound]);
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
                    cursor = WriteMetricName(buffer, cursor, metric.Name, metric.Unit);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_sum");

                    if (keys != null && keys.Length > 0)
                    {
                        buffer[cursor++] = unchecked((byte)'{');

                        for (var i = 0; i < keys.Length; i++)
                        {
                            if (i > 0)
                            {
                                buffer[cursor++] = unchecked((byte)',');
                            }

                            cursor = WriteLabel(buffer, cursor, keys[i], values[i]);
                        }

                        buffer[cursor++] = unchecked((byte)'}');
                    }

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteDouble(buffer, cursor, metricPoint.DoubleValue);
                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, timestamp);

                    buffer[cursor++] = ASCII_LINEFEED;

                    // Histogram count
                    cursor = WriteMetricName(buffer, cursor, metric.Name, metric.Unit);
                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "_count");

                    if (keys != null && keys.Length > 0)
                    {
                        buffer[cursor++] = unchecked((byte)'{');

                        for (var i = 0; i < keys.Length; i++)
                        {
                            if (i > 0)
                            {
                                buffer[cursor++] = unchecked((byte)',');
                            }

                            cursor = WriteLabel(buffer, cursor, keys[i], values[i]);
                        }

                        buffer[cursor++] = unchecked((byte)'}');
                    }

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, totalCount);
                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, timestamp);

                    buffer[cursor++] = ASCII_LINEFEED;
                }
            }

            buffer[cursor++] = ASCII_LINEFEED;

            return cursor;
        }
    }
}
