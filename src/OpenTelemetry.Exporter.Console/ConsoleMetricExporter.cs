// <copyright file="ConsoleMetricExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter
{
    public class ConsoleMetricExporter : ConsoleExporter<MetricItem>
    {
        private Resource resource;

        public ConsoleMetricExporter(ConsoleExporterOptions options)
            : base(options)
        {
        }

        public override ExportResult Export(in Batch<MetricItem> batch)
        {
            if (this.resource == null)
            {
                this.resource = this.ParentProvider.GetResource();
                if (this.resource != Resource.Empty)
                {
                    foreach (var resourceAttribute in this.resource.Attributes)
                    {
                        if (resourceAttribute.Key.Equals("service.name"))
                        {
                            Console.WriteLine("Service.Name" + resourceAttribute.Value);
                        }
                    }
                }
            }

            foreach (var metricItem in batch)
            {
                foreach (var metric in metricItem.Metrics)
                {
                    var tags = metric.Attributes.ToArray().Select(k => $"{k.Key}={k.Value?.ToString()}");

                    string valueDisplay = string.Empty;

                    // Switch would be faster than the if.else ladder
                    // of try and cast.
                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                            {
                                valueDisplay = (metric as ISumMetricLong).LongSum.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.DoubleSum:
                            {
                                valueDisplay = (metric as ISumMetricDouble).DoubleSum.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.LongGauge:
                            {
                                valueDisplay = (metric as IGaugeMetric).LastValue.Value.ToString();
                                break;
                            }

                        case MetricType.DoubleGauge:
                            {
                                valueDisplay = (metric as IGaugeMetric).LastValue.Value.ToString();
                                break;
                            }

                        case MetricType.Histogram:
                            {
                                var histogramMetric = metric as IHistogramMetric;
                                var bucketsBuilder = new StringBuilder();
                                bucketsBuilder.Append($"Sum: {histogramMetric.PopulationSum} Count: {histogramMetric.PopulationCount} \n");
                                foreach (var bucket in histogramMetric.Buckets)
                                {
                                    bucketsBuilder.Append('(');
                                    bucketsBuilder.Append(double.IsInfinity(bucket.LowBoundary) ? "-Infinity" : $"{bucket.LowBoundary}");
                                    bucketsBuilder.Append(", ");
                                    bucketsBuilder.Append(double.IsInfinity(bucket.HighBoundary) ? "+Infinity" : $"{bucket.HighBoundary}");
                                    bucketsBuilder.Append(double.IsInfinity(bucket.HighBoundary) ? ')' : ']');
                                    bucketsBuilder.Append($": {bucket.Count}");
                                    bucketsBuilder.AppendLine();
                                }

                                valueDisplay = bucketsBuilder.ToString();
                                break;
                            }

                        case MetricType.Summary:
                            {
                                var summaryMetric = metric as ISummaryMetric;
                                valueDisplay = string.Format("Sum: {0} Count: {1}", summaryMetric.PopulationSum, summaryMetric.PopulationCount);
                                break;
                            }
                    }

                    var msg = new StringBuilder($"Export (");
                    msg.Append(metric.StartTimeExclusive.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append(", ");
                    msg.Append(metric.EndTimeInclusive.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append("] ");
                    msg.Append(metric.Name);
                    msg.Append(' ');
                    msg.Append(string.Join(";", tags));
                    msg.Append(' ');
                    msg.Append(metric.MetricType);

                    if (!string.IsNullOrEmpty(metric.Description))
                    {
                        msg.Append($", Description: {metric.Description}");
                    }

                    if (!string.IsNullOrEmpty(metric.Unit))
                    {
                        msg.Append($", Unit: {metric.Unit}");
                    }

                    if (!string.IsNullOrEmpty(metric.Meter.Name))
                    {
                        msg.Append($", Meter: {metric.Meter.Name}");

                        if (!string.IsNullOrEmpty(metric.Meter.Version))
                        {
                            msg.Append($"/{metric.Meter.Version}");
                        }
                    }

                    msg.AppendLine();
                    msg.Append($"Value: {valueDisplay}");
                    Console.WriteLine(msg);
                }
            }

            return ExportResult.Success;
        }
    }
}
