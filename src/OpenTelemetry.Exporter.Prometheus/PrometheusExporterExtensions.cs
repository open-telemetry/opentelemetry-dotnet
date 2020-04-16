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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus.Implementation;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Helper to write metrics collection from exporter in Prometheus format.
    /// </summary>
    public static class PrometheusExporterExtensions
    {
        /// <summary>
        /// Serialize to Prometheus Format.
        /// </summary>
        /// <param name="exporter">Prometheus Exporter.</param>
        /// <param name="writer">StreamWriter to write to.</param>
        public static void WriteMetricsCollection(this PrometheusExporter exporter, StreamWriter writer)
        {
            foreach (var metric in exporter.GetAndClearDoubleMetrics())
            {
                var labels = metric.Labels;
                var builder = new PrometheusMetricBuilder()
                    .WithName(metric.MetricName)
                    .WithDescription(metric.MetricDescription);

                switch (metric.AggregationType)
                {
                    case AggregationType.DoubleSum:
                        {
                            var doubleSum = metric.Data as SumData<double>;
                            var doubleValue = doubleSum.Sum;

                            builder = builder.WithType("counter");

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
                            // Not supported yet.
                            break;
                        }
                }
            }

            foreach (var metric in exporter.GetAndClearLongMetrics())
            {
                var labels = metric.Labels;
                var builder = new PrometheusMetricBuilder()
                    .WithName(metric.MetricName)
                    .WithDescription(metric.MetricDescription);

                switch (metric.AggregationType)
                {
                    case AggregationType.LongSum:
                        {
                            var longSum = metric.Data as SumData<long>;
                            var longValue = longSum.Sum;
                            builder = builder.WithType("counter");

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
                            // Not supported yet.
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
