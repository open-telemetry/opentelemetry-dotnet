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

using System.Globalization;
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
                    this.WriteLine("Resource associated with Metric:");
                    foreach (var resourceAttribute in this.resource.Attributes)
                    {
                        if (ConsoleTagTransformer.Instance.TryTransformTag(resourceAttribute, out var result))
                        {
                            this.WriteLine($"    {result}");
                        }
                    }
                }
            }

            foreach (var metric in batch)
            {
                var msg = new StringBuilder($"\nExport ");
                msg.Append(metric.Name);
                if (metric.Description != string.Empty)
                {
                    msg.Append(", ");
                    msg.Append(metric.Description);
                }

                if (metric.Unit != string.Empty)
                {
                    msg.Append($", Unit: {metric.Unit}");
                }

                if (!string.IsNullOrEmpty(metric.MeterName))
                {
                    msg.Append($", Meter: {metric.MeterName}");

                    if (!string.IsNullOrEmpty(metric.MeterVersion))
                    {
                        msg.Append($"/{metric.MeterVersion}");
                    }
                }

                this.WriteLine(msg.ToString());

                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    string valueDisplay = string.Empty;
                    StringBuilder tagsBuilder = new StringBuilder();
                    foreach (var tag in metricPoint.Tags)
                    {
                        if (ConsoleTagTransformer.Instance.TryTransformTag(tag, out var result))
                        {
                            tagsBuilder.Append(result);
                            tagsBuilder.Append(' ');
                        }
                    }

                    var tags = tagsBuilder.ToString().TrimEnd();

                    var metricType = metric.MetricType;

                    if (metricType.IsHistogram())
                    {
                        var bucketsBuilder = new StringBuilder();
                        var sum = metricPoint.GetHistogramSum();
                        var count = metricPoint.GetHistogramCount();
                        if (metricPoint.HasMinMax())
                        {
                            bucketsBuilder.Append($"Sum: {sum} Count: {count} Min: {metricPoint.GetHistogramMin()} Max: {metricPoint.GetHistogramMax()} \n");
                        }
                        else
                        {
                            bucketsBuilder.Append($"Sum: {sum} Count: {count} \n");
                        }

                        bool isFirstIteration = true;
                        double previousExplicitBound = default;
                        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                        {
                            if (isFirstIteration)
                            {
                                bucketsBuilder.Append("(-Infinity,");
                                bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                                previousExplicitBound = histogramMeasurement.ExplicitBound;
                                isFirstIteration = false;
                            }
                            else
                            {
                                bucketsBuilder.Append('(');
                                bucketsBuilder.Append(previousExplicitBound);
                                bucketsBuilder.Append(',');
                                if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                                {
                                    bucketsBuilder.Append(histogramMeasurement.ExplicitBound);
                                }
                                else
                                {
                                    bucketsBuilder.Append("+Infinity");
                                }

                                bucketsBuilder.Append(']');
                                bucketsBuilder.Append(':');
                                bucketsBuilder.Append(histogramMeasurement.BucketCount);
                            }

                            bucketsBuilder.AppendLine();
                        }

                        valueDisplay = bucketsBuilder.ToString();
                    }
                    else if (metricType.IsDouble())
                    {
                        if (metricType.IsSum())
                        {
                            valueDisplay = metricPoint.GetSumDouble().ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            valueDisplay = metricPoint.GetGaugeLastValueDouble().ToString(CultureInfo.InvariantCulture);
                        }
                    }
                    else if (metricType.IsLong())
                    {
                        if (metricType.IsSum())
                        {
                            valueDisplay = metricPoint.GetSumLong().ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            valueDisplay = metricPoint.GetGaugeLastValueLong().ToString(CultureInfo.InvariantCulture);
                        }
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
                    this.WriteLine(msg.ToString());
                }
            }

            return ExportResult.Success;
        }
    }
}
