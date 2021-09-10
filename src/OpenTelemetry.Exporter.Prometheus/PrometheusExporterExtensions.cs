// <copyright file="PrometheusExporterExtensions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Helper to write metrics collection from exporter in Prometheus format.
    /// </summary>
    public static class PrometheusExporterExtensions
    {
        private const string PrometheusCounterType = "counter";
        private const string PrometheusGaugeType = "gauge";
        private const string PrometheusHistogramType = "histogram";
        private const string PrometheusHistogramSumPostFix = "_sum";
        private const string PrometheusHistogramCountPostFix = "_count";
        private const string PrometheusHistogramBucketPostFix = "_bucket";
        private const string PrometheusHistogramBucketLabelPositiveInfinity = "+Inf";
        private const string PrometheusHistogramBucketLabelLessThan = "le";

        /// <summary>
        /// Serialize to Prometheus Format.
        /// </summary>
        /// <param name="exporter">Prometheus Exporter.</param>
        /// <param name="writer">StreamWriter to write to.</param>
        public static void WriteMetricsCollection(this PrometheusExporter exporter, StreamWriter writer)
        {
            foreach (var metric in exporter.Metrics)
            {
                var builder = new PrometheusMetricBuilder()
                    .WithName(metric.Name)
                    .WithDescription(metric.Description);

                switch (metric.MetricType)
                {
                    case MetricType.LongSum:
                        {
                            builder = builder.WithType(PrometheusCounterType);
                            foreach (ref var metricPoint in metric.GetMetricPoints())
                            {
                                var metricValueBuilder = builder.AddValue();
                                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.LongValue);
                                if (metricPoint.Keys != null)
                                {
                                    for (int i = 0; i < metricPoint.Keys.Length; i++)
                                    {
                                        metricValueBuilder.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                    }
                                }
                            }

                            builder.Write(writer);
                            break;
                        }

                    case MetricType.DoubleSum:
                        {
                            builder = builder.WithType(PrometheusCounterType);
                            foreach (ref var metricPoint in metric.GetMetricPoints())
                            {
                                var metricValueBuilder = builder.AddValue();
                                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.DoubleValue);
                                if (metricPoint.Keys != null)
                                {
                                    for (int i = 0; i < metricPoint.Keys.Length; i++)
                                    {
                                        metricValueBuilder.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                    }
                                }
                            }

                            builder.Write(writer);
                            break;
                        }

                    case MetricType.LongGauge:
                        {
                            builder = builder.WithType(PrometheusGaugeType);
                            foreach (ref var metricPoint in metric.GetMetricPoints())
                            {
                                var metricValueBuilder = builder.AddValue();
                                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.LongValue);
                                for (int i = 0; i < metricPoint.Keys.Length; i++)
                                {
                                    metricValueBuilder.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                }
                            }

                            builder.Write(writer);
                            break;
                        }

                    case MetricType.DoubleGauge:
                        {
                            builder = builder.WithType(PrometheusGaugeType);
                            foreach (ref var metricPoint in metric.GetMetricPoints())
                            {
                                var metricValueBuilder = builder.AddValue();
                                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.DoubleValue);
                                for (int i = 0; i < metricPoint.Keys.Length; i++)
                                {
                                    metricValueBuilder.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                }
                            }

                            builder.Write(writer);
                            break;
                        }

                    case MetricType.Histogram:
                        {
                            /*
                             *  For Histogram we emit one row for Sum, Count and as
                             *  many rows as number of buckets.
                             *  myHistogram_sum{tag1="value1",tag2="value2"} 258330 1629860660991
                             *  myHistogram_count{tag1="value1",tag2="value2"} 355 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="0"} 0 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="5"} 2 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="10"} 4 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="25"} 6 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="50"} 12 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="75"} 19 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="100"} 26 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="250"} 65 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="500"} 128 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="1000"} 241 1629860660991
                             *  myHistogram_bucket{tag1="value1",tag2="value2",le="+Inf"} 355 1629860660991
                            */
                            builder = builder.WithType(PrometheusHistogramType);
                            foreach (ref var metricPoint in metric.GetMetricPoints())
                            {
                                var metricValueBuilderSum = builder.AddValue();
                                metricValueBuilderSum.WithName(metric.Name + PrometheusHistogramSumPostFix);
                                metricValueBuilderSum = metricValueBuilderSum.WithValue(metricPoint.DoubleValue);
                                for (int i = 0; i < metricPoint.Keys.Length; i++)
                                {
                                    metricValueBuilderSum.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                }

                                var metricValueBuilderCount = builder.AddValue();
                                metricValueBuilderCount.WithName(metric.Name + PrometheusHistogramCountPostFix);
                                metricValueBuilderCount = metricValueBuilderCount.WithValue(metricPoint.LongValue);
                                for (int i = 0; i < metricPoint.Keys.Length; i++)
                                {
                                    metricValueBuilderCount.WithLabel(metricPoint.Keys[i], metricPoint.Values[i].ToString());
                                }

                                long totalCount = 0;
                                for (int i = 0; i < metricPoint.ExplicitBounds.Length + 1; i++)
                                {
                                    totalCount += metricPoint.BucketCounts[i];
                                    var metricValueBuilderBuckets = builder.AddValue();
                                    metricValueBuilderBuckets.WithName(metric.Name + PrometheusHistogramBucketPostFix);
                                    metricValueBuilderBuckets = metricValueBuilderBuckets.WithValue(totalCount);
                                    for (int j = 0; j < metricPoint.Keys.Length; j++)
                                    {
                                        metricValueBuilderBuckets.WithLabel(metricPoint.Keys[j], metricPoint.Values[j].ToString());
                                    }

                                    var bucketName = i == metricPoint.ExplicitBounds.Length ?
                                    PrometheusHistogramBucketLabelPositiveInfinity : metricPoint.ExplicitBounds[i].ToString(CultureInfo.InvariantCulture);
                                    metricValueBuilderBuckets.WithLabel(PrometheusHistogramBucketLabelLessThan, bucketName);
                                }
                            }

                            builder.Write(writer);

                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Get Metrics Collection as a string.
        /// </summary>
        /// <param name="exporter"> Prometheus Exporter. </param>
        /// <returns>Metrics serialized to string in Prometheus format.</returns>
        public static string GetMetricsCollection(this PrometheusExporter exporter)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            WriteMetricsCollection(exporter, writer);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray(), 0, (int)stream.Length);
        }
    }
}
