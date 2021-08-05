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
        private const string PrometheusSummaryType = "summary";
        private const string PrometheusSummarySumPostFix = "_sum";
        private const string PrometheusSummaryCountPostFix = "_count";
        private const string PrometheusSummaryQuantileLabelName = "quantile";
        private const string PrometheusSummaryQuantileLabelValueForMin = "0";
        private const string PrometheusSummaryQuantileLabelValueForMax = "1";

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
                    .WithDescription(metric.Name);

                    // TODO: Use switch case for higher perf.
                    if (metric.MetricType == MetricType.LongSum)
                    {
                        WriteSum(writer, builder, metric.Attributes, (metric as ISumMetricLong).LongSum);
                    }
                    else if (metric.MetricType == MetricType.DoubleSum)
                    {
                        WriteSum(writer, builder, metric.Attributes, (metric as ISumMetricDouble).DoubleSum);
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

        private static void WriteSummary(
            StreamWriter writer,
            PrometheusMetricBuilder builder,
            IEnumerable<KeyValuePair<string, string>> labels,
            string metricName,
            double sum,
            long count,
            double min,
            double max)
        {
            builder = builder.WithType(PrometheusSummaryType);

            foreach (var label in labels)
            {
                /* For Summary we emit one row for Sum, Count, Min, Max.
                Min,Max exports as quantile 0 and 1.
                In future, when OpenTelemetry implements more aggregation
                algorithms, this section will need to be revisited.
                Sample output:
                MyMeasure_sum{dim1="value1"} 750 1587013352982
                MyMeasure_count{dim1="value1"} 5 1587013352982
                MyMeasure{dim1="value2",quantile="0"} 150 1587013352982
                MyMeasure{dim1="value2",quantile="1"} 150 1587013352982
                */
                builder.AddValue()
                    .WithName(metricName + PrometheusSummarySumPostFix)
                    .WithLabel(label.Key, label.Value)
                    .WithValue(sum);
                builder.AddValue()
                    .WithName(metricName + PrometheusSummaryCountPostFix)
                    .WithLabel(label.Key, label.Value)
                    .WithValue(count);
                builder.AddValue()
                    .WithName(metricName)
                    .WithLabel(label.Key, label.Value)
                    .WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMin)
                    .WithValue(min);
                builder.AddValue()
                    .WithName(metricName)
                    .WithLabel(label.Key, label.Value)
                    .WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMax)
                    .WithValue(max);
            }

            builder.Write(writer);
        }
    }
}
