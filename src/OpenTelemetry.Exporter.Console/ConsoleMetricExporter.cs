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
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter
{
    [AggregationTemporality(AggregationTemporality.Cumulative | AggregationTemporality.Delta, AggregationTemporality.Cumulative)]
    public class ConsoleMetricExporter : ConsoleExporter<Metric>
    {
        private byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
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
                            this.WriteLine("Service.Name" + resourceAttribute.Value);
                        }
                    }
                }
            }

            /*
            foreach (var metric in batch)
            {
                var msg = new StringBuilder($"\nExport ");
                msg.Append(metric.Name);
                if (!string.IsNullOrEmpty(metric.Description))
                {
                    msg.Append(", ");
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

                foreach (ref var metricPoint in metric.GetMetricPoints())
                {
                    string valueDisplay = string.Empty;
                    StringBuilder tagsBuilder = new StringBuilder();
                    if (metricPoint.Keys != null)
                    {
                        for (int i = 0; i < metricPoint.Keys.Length; i++)
                        {
                            tagsBuilder.Append(metricPoint.Keys[i]);
                            tagsBuilder.Append(':');
                            tagsBuilder.Append(metricPoint.Values[i]);
                            tagsBuilder.Append(' ');
                        }
                    }

                    var tags = tagsBuilder.ToString().TrimEnd();

                    var metricType = metric.MetricType;

                    if (metricType.IsHistogram())
                    {
                        var bucketsBuilder = new StringBuilder();
                        bucketsBuilder.Append($"Sum: {metricPoint.DoubleValue} Count: {metricPoint.LongValue} \n");

                        if (metricPoint.ExplicitBounds != null)
                        {
                            for (int i = 0; i < metricPoint.ExplicitBounds.Length + 1; i++)
                            {
                                if (i == 0)
                                {
                                    bucketsBuilder.Append("(-Infinity,");
                                    bucketsBuilder.Append(metricPoint.ExplicitBounds[i]);
                                    bucketsBuilder.Append(']');
                                    bucketsBuilder.Append(':');
                                    bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                }
                                else if (i == metricPoint.ExplicitBounds.Length)
                                {
                                    bucketsBuilder.Append('(');
                                    bucketsBuilder.Append(metricPoint.ExplicitBounds[i - 1]);
                                    bucketsBuilder.Append(',');
                                    bucketsBuilder.Append("+Infinity]");
                                    bucketsBuilder.Append(':');
                                    bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                }
                                else
                                {
                                    bucketsBuilder.Append('(');
                                    bucketsBuilder.Append(metricPoint.ExplicitBounds[i - 1]);
                                    bucketsBuilder.Append(',');
                                    bucketsBuilder.Append(metricPoint.ExplicitBounds[i]);
                                    bucketsBuilder.Append(']');
                                    bucketsBuilder.Append(':');
                                    bucketsBuilder.Append(metricPoint.BucketCounts[i]);
                                }

                                bucketsBuilder.AppendLine();
                            }
                        }

                        valueDisplay = bucketsBuilder.ToString();
                    }
                    else if (metricType.IsDouble())
                    {
                        valueDisplay = metricPoint.DoubleValue.ToString(CultureInfo.InvariantCulture);
                    }
                    else if (metricType.IsLong())
                    {
                        valueDisplay = metricPoint.LongValue.ToString(CultureInfo.InvariantCulture);
                    }

                    msg = new StringBuilder();
                    msg.Append('(');
                    msg.Append(metricPoint.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append(", ");
                    msg.Append(metricPoint.EndTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                    msg.Append("] ");
                    msg.Append(tags);
                    if (tags != string.Empty)
                    {
                        msg.Append(' ');
                    }

                    msg.Append(metric.MetricType);
                    msg.AppendLine();
                    msg.Append($"Value: {valueDisplay}");
                    Console.WriteLine(msg);
                }
            }
            */

            int cursor = 0;

            try
            {
                foreach (var metric in batch)
                {
                    while (true)
                    {
                        try
                        {
                            cursor = OpenTelemetry.Exporter.Prometheus.PrometheusSerializer.WriteMetric(this.buffer, cursor, metric);
                            break;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            int bufferSize = this.buffer.Length * 2;

                            // there are two cases we might run into the following condition:
                            // 1. we have many metrics to be exported - in this case we probably want
                            //    to put some upper limit and allow the user to configure it.
                            // 2. we got an IndexOutOfRangeException which was triggered by some other
                            //    code instead of the buffer[cursor++] - in this case we should give up
                            //    at certain point rather than allocating like crazy.
                            if (bufferSize > 100 * 1024 * 1024)
                            {
                                throw;
                            }

                            var newBuffer = new byte[bufferSize];
                            this.buffer.CopyTo(newBuffer, 0);
                            this.buffer = newBuffer;
                        }
                    }
                }

                this.WriteLine(Encoding.UTF8.GetString(this.buffer, 0, Math.Max(cursor - 1, 0)));

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                return ExportResult.Failure;
            }
        }
    }
}
