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
    public class ConsoleMetricExporter : ConsoleExporter<Metric>
    {
        private Resource resource;

        public ConsoleMetricExporter(ConsoleExporterOptions options)
            : base(options)
        {
        }

        public override ExportResult Export(in Batch<Metric> batch)
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

            foreach (var metric in batch)
            {
                var msg = new StringBuilder($"\nExport ");
                msg.Append(metric.Name);
                if (!string.IsNullOrEmpty(metric.Description))
                {
                    msg.Append(' ');
                    msg.Append(metric.Description);
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

                Console.WriteLine(msg.ToString());

                foreach (var metricPoint in metric.GetMetricPoints())
                {
                    string valueDisplay = string.Empty;
                    StringBuilder tagsBuilder = new StringBuilder();
                    for (int i = 0; i < metricPoint.Keys.Length; i++)
                    {
                        tagsBuilder.Append(metricPoint.Keys[i]);
                        tagsBuilder.Append(":");
                        tagsBuilder.Append(metricPoint.Values[i]);
                    }

                    var tags = tagsBuilder.ToString();

                    // Switch would be faster than the if.else ladder
                    // of try and cast.
                    switch (metric.MetricType)
                    {
                        case MetricType.LongSum:
                            {
                                valueDisplay = metricPoint.LongValue.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.DoubleSum:
                            {
                                valueDisplay = metricPoint.DoubleValue.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.LongGauge:
                            {
                                valueDisplay = metricPoint.LongValue.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.DoubleGauge:
                            {
                                valueDisplay = metricPoint.DoubleValue.ToString(CultureInfo.InvariantCulture);
                                break;
                            }

                        case MetricType.Histogram:
                            {
                                var bucketsBuilder = new StringBuilder();
                                bucketsBuilder.Append($"Sum: {metricPoint.DoubleValue} Count: {metricPoint.LongValue} \n");
                                for (int i = 0; i < metricPoint.ExplicitBounds.Length + 1; i++)
                                {
                                    if (i == 0)
                                    {
                                        bucketsBuilder.Append("(-Infinity,");
                                        bucketsBuilder.Append(metricPoint.ExplicitBounds[i]);
                                        bucketsBuilder.Append("]");
                                        bucketsBuilder.Append(":");
                                        bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                    }
                                    else if (i == metricPoint.ExplicitBounds.Length)
                                    {
                                        bucketsBuilder.Append("(");
                                        bucketsBuilder.Append(metricPoint.ExplicitBounds[i - 1]);
                                        bucketsBuilder.Append(",");
                                        bucketsBuilder.Append("+Infinity]");
                                        bucketsBuilder.Append(":");
                                        bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                    }
                                    else
                                    {
                                        bucketsBuilder.Append("(");
                                        bucketsBuilder.Append(metricPoint.ExplicitBounds[i - 1]);
                                        bucketsBuilder.Append(",");
                                        bucketsBuilder.Append(metricPoint.ExplicitBounds[i]);
                                        bucketsBuilder.Append("]");
                                        bucketsBuilder.Append(":");
                                        bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                    }

                                    bucketsBuilder.AppendLine();
                                }

                                valueDisplay = bucketsBuilder.ToString();
                                break;
                            }
                    }

                    msg = new StringBuilder();
                    msg.Append(metricPoint.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append(", ");
                    msg.Append(metricPoint.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append("] ");
                    msg.Append(string.Join(";", tags));
                    msg.Append(' ');
                    msg.Append(metric.MetricType);
                    msg.AppendLine();
                    msg.Append($"Value: {valueDisplay}");
                    Console.WriteLine(msg);
                }
            }

            return ExportResult.Success;
        }
    }
}
