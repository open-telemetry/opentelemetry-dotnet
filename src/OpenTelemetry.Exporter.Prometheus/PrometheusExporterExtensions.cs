// <copyright file="PrometheusExporterExtensions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System.IO;
using System.Text;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Prometheus
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
            foreach (var metric in exporter.GetAndClearDoubleMetrics())
            {
                var builder = new PrometheusMetricBuilder()
                    .WithName(metric.MetricName)
                    .WithDescription(metric.MetricDescription);

                foreach (var metricData in metric.Data)
                {
                    var labels = metricData.Labels;
                    switch (metric.AggregationType)
                    {
                        case AggregationType.DoubleSum:
                            {
                                var doubleSum = metricData as SumData<double>;
                                var doubleValue = doubleSum.Sum;

                                builder = builder.WithType(PrometheusCounterType);

                                foreach (var label in labels)
                                {
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder = metricValueBuilder.WithValue(doubleValue);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                }

                                builder.Write(writer);
                                break;
                            }

                        case AggregationType.Summary:
                            {
                                var longSummary = metricData as SummaryData<double>;
                                var longValueCount = longSummary.Count;
                                var longValueSum = longSummary.Sum;
                                var longValueMin = longSummary.Min;
                                var longValueMax = longSummary.Max;

                                builder = builder.WithType(PrometheusSummaryType);

                                foreach (var label in labels)
                                {
                                    /* For Summary we emit one row for Sum, Count, Min, Max.
                                    Min,Max exportes as quantile 0 and 1.
                                    In future, when OT implements more aggregation algorithms,
                                    this section will need to be revisited.
                                    Sample output:
                                    MyMeasure_sum{dim1="value1"} 750 1587013352982
                                    MyMeasure_count{dim1="value1"} 5 1587013352982
                                    MyMeasure{dim1="value2",quantile="0"} 150 1587013352982
                                    MyMeasure{dim1="value2",quantile="1"} 150 1587013352982
                                    */
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName + PrometheusSummarySumPostFix);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueSum);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName + PrometheusSummaryCountPostFix);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueCount);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueMin);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                    metricValueBuilder.WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMin);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueMax);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                    metricValueBuilder.WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMax);
                                }

                                builder.Write(writer);
                                break;
                            }
                    }
                }
            }

            foreach (var metric in exporter.GetAndClearLongMetrics())
            {
                var builder = new PrometheusMetricBuilder()
                    .WithName(metric.MetricName)
                    .WithDescription(metric.MetricDescription);

                foreach (var metricData in metric.Data)
                {
                    var labels = metricData.Labels;
                    switch (metric.AggregationType)
                    {
                        case AggregationType.LongSum:
                            {
                                var longSum = metricData as SumData<long>;
                                var longValue = longSum.Sum;
                                builder = builder.WithType(PrometheusCounterType);

                                foreach (var label in labels)
                                {
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder = metricValueBuilder.WithValue(longValue);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                }

                                builder.Write(writer);
                                break;
                            }

                        case AggregationType.Summary:
                            {
                                var longSummary = metricData as SummaryData<long>;
                                var longValueCount = longSummary.Count;
                                var longValueSum = longSummary.Sum;
                                var longValueMin = longSummary.Min;
                                var longValueMax = longSummary.Max;

                                builder = builder.WithType(PrometheusSummaryType);

                                foreach (var label in labels)
                                {
                                    /* For Summary we emit one row for Sum, Count, Min, Max.
                                    Min,Max exportes as quantile 0 and 1.
                                    In future, when OT implements more aggregation algorithms,
                                    this section will need to be revisited.
                                    Sample output:
                                    MyMeasure_sum{dim1="value1"} 750 1587013352982
                                    MyMeasure_count{dim1="value1"} 5 1587013352982
                                    MyMeasure{dim1="value2",quantile="0"} 150 1587013352982
                                    MyMeasure{dim1="value2",quantile="1"} 150 1587013352982
                                    */
                                    var metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName + PrometheusSummarySumPostFix);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueSum);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName + PrometheusSummaryCountPostFix);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueCount);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueMin);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                    metricValueBuilder.WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMin);

                                    metricValueBuilder = builder.AddValue();
                                    metricValueBuilder.WithName(metric.MetricName);
                                    metricValueBuilder = metricValueBuilder.WithValue(longValueMax);
                                    metricValueBuilder.WithLabel(label.Key, label.Value);
                                    metricValueBuilder.WithLabel(PrometheusSummaryQuantileLabelName, PrometheusSummaryQuantileLabelValueForMax);
                                }

                                builder.Write(writer);
                                break;
                            }
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
