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

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using static OpenTelemetry.Exporter.Prometheus.PrometheusMetricBuilder;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Helper to write metrics collection from exporter in Prometheus format.
    /// </summary>
    internal static class PrometheusExporterExtensions
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
        /// Serialize metrics to prometheus format.
        /// </summary>
        /// <param name="exporter"><see cref="PrometheusExporter"/>.</param>
        /// <param name="writer">StreamWriter to write to.</param>
        /// <param name="getUtcNowDateTimeOffset">Optional function to resolve the current date &amp; time.</param>
        /// <returns><see cref="Task"/> to await the operation.</returns>
        public static async Task WriteMetricsCollection(this PrometheusExporter exporter, StreamWriter writer, Func<DateTimeOffset> getUtcNowDateTimeOffset)
        {
            foreach (var metric in exporter.Metrics)
            {
                var builder = new PrometheusMetricBuilder(getUtcNowDateTimeOffset)
                    .WithName(metric.Name)
                    .WithDescription(metric.Description);

                switch (metric.MetricType)
                {
                    case MetricType.LongSum:
                        {
                            builder = builder.WithType(PrometheusCounterType);
                            WriteLongSumMetrics(metric, builder);
                            break;
                        }

                    case MetricType.DoubleSum:
                        {
                            builder = builder.WithType(PrometheusCounterType);
                            WriteDoubleSumMetrics(metric, builder);
                            break;
                        }

                    case MetricType.LongGauge:
                        {
                            builder = builder.WithType(PrometheusGaugeType);
                            WriteLongGaugeMetrics(metric, builder);
                            break;
                        }

                    case MetricType.DoubleGauge:
                        {
                            builder = builder.WithType(PrometheusGaugeType);
                            WriteDoubleGaugeMetrics(metric, builder);
                            break;
                        }

                    case MetricType.Histogram:
                        {
                            builder = builder.WithType(PrometheusHistogramType);
                            WriteHistogramMetrics(metric, builder);
                            break;
                        }
                }

                await builder.Write(writer).ConfigureAwait(false);
            }
        }

        private static void WriteLongSumMetrics(Metric metric, PrometheusMetricBuilder builder)
        {
            foreach (ref var metricPoint in metric.GetMetricPoints())
            {
                var metricValueBuilder = builder.AddValue();
                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.LongValue);
                metricValueBuilder.AddLabels(metricPoint.Keys, metricPoint.Values);
            }
        }

        private static void WriteDoubleSumMetrics(Metric metric, PrometheusMetricBuilder builder)
        {
            foreach (ref var metricPoint in metric.GetMetricPoints())
            {
                var metricValueBuilder = builder.AddValue();
                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.DoubleValue);
                metricValueBuilder.AddLabels(metricPoint.Keys, metricPoint.Values);
            }
        }

        private static void WriteLongGaugeMetrics(Metric metric, PrometheusMetricBuilder builder)
        {
            foreach (ref var metricPoint in metric.GetMetricPoints())
            {
                var metricValueBuilder = builder.AddValue();
                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.LongValue);
                metricValueBuilder.AddLabels(metricPoint.Keys, metricPoint.Values);
            }
        }

        private static void WriteDoubleGaugeMetrics(Metric metric, PrometheusMetricBuilder builder)
        {
            foreach (ref var metricPoint in metric.GetMetricPoints())
            {
                var metricValueBuilder = builder.AddValue();
                metricValueBuilder = metricValueBuilder.WithValue(metricPoint.DoubleValue);
                metricValueBuilder.AddLabels(metricPoint.Keys, metricPoint.Values);
            }
        }

        private static void WriteHistogramMetrics(Metric metric, PrometheusMetricBuilder builder)
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

            foreach (ref var metricPoint in metric.GetMetricPoints())
            {
                var metricValueBuilderSum = builder.AddValue();
                metricValueBuilderSum.WithName(metric.Name + PrometheusHistogramSumPostFix);
                metricValueBuilderSum = metricValueBuilderSum.WithValue(metricPoint.DoubleValue);
                metricValueBuilderSum.AddLabels(metricPoint.Keys, metricPoint.Values);

                var metricValueBuilderCount = builder.AddValue();
                metricValueBuilderCount.WithName(metric.Name + PrometheusHistogramCountPostFix);
                metricValueBuilderCount = metricValueBuilderCount.WithValue(metricPoint.LongValue);
                metricValueBuilderCount.AddLabels(metricPoint.Keys, metricPoint.Values);

                if (metricPoint.ExplicitBounds != null)
                {
                    long totalCount = 0;
                    for (int i = 0; i < metricPoint.ExplicitBounds.Length + 1; i++)
                    {
                        totalCount += metricPoint.BucketCounts[i];
                        var metricValueBuilderBuckets = builder.AddValue();
                        metricValueBuilderBuckets.WithName(metric.Name + PrometheusHistogramBucketPostFix);
                        metricValueBuilderBuckets = metricValueBuilderBuckets.WithValue(totalCount);
                        metricValueBuilderBuckets.AddLabels(metricPoint.Keys, metricPoint.Values);

                        var bucketName = i == metricPoint.ExplicitBounds.Length ?
                        PrometheusHistogramBucketLabelPositiveInfinity : metricPoint.ExplicitBounds[i].ToString(CultureInfo.InvariantCulture);
                        metricValueBuilderBuckets.WithLabel(PrometheusHistogramBucketLabelLessThan, bucketName);
                    }
                }
            }
        }

        private static void AddLabels(this PrometheusMetricValueBuilder valueBuilder, string[] keys, object[] values)
        {
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    valueBuilder.WithLabel(keys[i], values[i].ToString());
                }
            }
        }
    }
}
