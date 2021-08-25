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
            foreach (var metricItem in exporter.Batch)
            {
                foreach (var metric in metricItem.Metrics)
                {
                    var builder = new PrometheusMetricBuilder()
                    .WithName(metric.Name)
                    .WithDescription(metric.Description);

                    // TODO: Use switch case for higher perf.
                    if (metric.MetricType == MetricType.LongSum)
                    {
                        WriteSum(writer, builder, metric.Attributes, (metric as ISumMetricLong).LongSum);
                    }
                    else if (metric.MetricType == MetricType.DoubleSum)
                    {
                        WriteSum(writer, builder, metric.Attributes, (metric as ISumMetricDouble).DoubleSum);
                    }
                    else if (metric.MetricType == MetricType.DoubleGauge)
                    {
                        var gaugeMetric = metric as IGaugeMetric;
                        var doubleValue = (double)gaugeMetric.LastValue.Value;
                        WriteGauge(writer, builder, metric.Attributes, doubleValue);
                    }
                    else if (metric.MetricType == MetricType.LongGauge)
                    {
                        var gaugeMetric = metric as IGaugeMetric;
                        var longValue = (long)gaugeMetric.LastValue.Value;

                        // TODO: Prometheus only supports float/double
                        WriteGauge(writer, builder, metric.Attributes, longValue);
                    }
                    else if (metric.MetricType == MetricType.Histogram)
                    {
                        var histogramMetric = metric as IHistogramMetric;
                        WriteHistogram(writer, builder, metric.Attributes, metric.Name, histogramMetric.PopulationSum, histogramMetric.PopulationCount, histogramMetric.Buckets);
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

        private static void WriteSum(StreamWriter writer, PrometheusMetricBuilder builder, IEnumerable<KeyValuePair<string, object>> labels, double doubleValue)
        {
            builder = builder.WithType(PrometheusCounterType);

            var metricValueBuilder = builder.AddValue();
            metricValueBuilder = metricValueBuilder.WithValue(doubleValue);

            foreach (var label in labels)
            {
                metricValueBuilder.WithLabel(label.Key, label.Value.ToString());
            }

            builder.Write(writer);
        }

        private static void WriteGauge(StreamWriter writer, PrometheusMetricBuilder builder, IEnumerable<KeyValuePair<string, object>> labels, double doubleValue)
        {
            builder = builder.WithType(PrometheusGaugeType);

            var metricValueBuilder = builder.AddValue();
            metricValueBuilder = metricValueBuilder.WithValue(doubleValue);

            foreach (var label in labels)
            {
                metricValueBuilder.WithLabel(label.Key, label.Value.ToString());
            }

            builder.Write(writer);
        }

        private static void WriteHistogram(
            StreamWriter writer,
            PrometheusMetricBuilder builder,
            IEnumerable<KeyValuePair<string, object>> labels,
            string metricName,
            double sum,
            long count,
            IEnumerable<HistogramBucket> buckets)
        {
            /*  For Histogram we emit one row for Sum, Count and as
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
            var metricValueBuilderSum = builder.AddValue();
            metricValueBuilderSum.WithName(metricName + PrometheusHistogramSumPostFix);
            metricValueBuilderSum = metricValueBuilderSum.WithValue(sum);
            foreach (var label in labels)
            {
                metricValueBuilderSum.WithLabel(label.Key, label.Value.ToString());
            }

            var metricValueBuilderCount = builder.AddValue();
            metricValueBuilderCount.WithName(metricName + PrometheusHistogramCountPostFix);
            metricValueBuilderCount = metricValueBuilderCount.WithValue(count);
            foreach (var label in labels)
            {
                metricValueBuilderCount.WithLabel(label.Key, label.Value.ToString());
            }

            long totalCount = 0;
            foreach (var bucket in buckets)
            {
                totalCount += bucket.Count;
                var metricValueBuilderBuckets = builder.AddValue();
                metricValueBuilderBuckets.WithName(metricName + PrometheusHistogramBucketPostFix);
                metricValueBuilderBuckets = metricValueBuilderBuckets.WithValue(totalCount);
                foreach (var label in labels)
                {
                    metricValueBuilderBuckets.WithLabel(label.Key, label.Value.ToString());
                }

                var bucketName = double.IsPositiveInfinity(bucket.HighBoundary) ?
                    PrometheusHistogramBucketLabelPositiveInfinity : bucket.HighBoundary.ToString(CultureInfo.InvariantCulture);
                metricValueBuilderBuckets.WithLabel(PrometheusHistogramBucketLabelLessThan, bucketName);
            }

            builder.Write(writer);
        }
    }
}
