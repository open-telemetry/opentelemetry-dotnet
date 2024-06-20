// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

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
                    if (this.TagWriter.TryTransformTag(resourceAttribute, out var result))
                    {
                        this.WriteLine($"    {result.Key}: {result.Value}");
                    }
                }
            }
        }

        foreach (var metric in batch)
        {
            var msg = new StringBuilder($"\n");
            msg.Append($"Metric Name: {metric.Name}");
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

            if (metric.MeterTags != null)
            {
                foreach (var meterTag in metric.MeterTags)
                {
                    this.WriteLine("\tMeter Tags:");
                    if (this.TagWriter.TryTransformTag(meterTag, out var result))
                    {
                        this.WriteLine($"\t\t{result.Key}: {result.Value}");
                    }
                }
            }

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                string valueDisplay = string.Empty;
                StringBuilder tagsBuilder = new StringBuilder();
                foreach (var tag in metricPoint.Tags)
                {
                    if (this.TagWriter.TryTransformTag(tag, out var result))
                    {
                        tagsBuilder.Append($"{result.Key}: {result.Value}");
                        tagsBuilder.Append(' ');
                    }
                }

                var tags = tagsBuilder.ToString().TrimEnd();

                var metricType = metric.MetricType;

                if (metricType == MetricType.Histogram || metricType == MetricType.ExponentialHistogram)
                {
                    var bucketsBuilder = new StringBuilder();
                    var sum = metricPoint.GetHistogramSum();
                    var count = metricPoint.GetHistogramCount();
                    bucketsBuilder.Append($"Sum: {sum} Count: {count} ");
                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        bucketsBuilder.Append($"Min: {min} Max: {max} ");
                    }

                    bucketsBuilder.AppendLine();

                    if (metricType == MetricType.Histogram)
                    {
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
                                    previousExplicitBound = histogramMeasurement.ExplicitBound;
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
                    }
                    else
                    {
                        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();
                        var scale = exponentialHistogramData.Scale;

                        if (exponentialHistogramData.ZeroCount != 0)
                        {
                            bucketsBuilder.AppendLine($"Zero Bucket:{exponentialHistogramData.ZeroCount}");
                        }

                        var offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(offset, scale).ToString(CultureInfo.InvariantCulture);
                            var upperBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(++offset, scale).ToString(CultureInfo.InvariantCulture);
                            bucketsBuilder.AppendLine($"({lowerBound}, {upperBound}]:{bucketCount}");
                        }
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

                var exemplarString = new StringBuilder();
                if (metricPoint.TryGetExemplars(out var exemplars))
                {
                    foreach (ref readonly var exemplar in exemplars)
                    {
                        exemplarString.Append("Timestamp: ");
                        exemplarString.Append(exemplar.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
                        if (metricType.IsDouble())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.DoubleValue);
                        }
                        else if (metricType.IsLong())
                        {
                            exemplarString.Append(" Value: ");
                            exemplarString.Append(exemplar.LongValue);
                        }

                        if (exemplar.TraceId != default)
                        {
                            exemplarString.Append(" TraceId: ");
                            exemplarString.Append(exemplar.TraceId.ToHexString());
                            exemplarString.Append(" SpanId: ");
                            exemplarString.Append(exemplar.SpanId.ToHexString());
                        }

                        bool appendedTagString = false;
                        foreach (var tag in exemplar.FilteredTags)
                        {
                            if (this.TagWriter.TryTransformTag(tag, out var result))
                            {
                                if (!appendedTagString)
                                {
                                    exemplarString.Append(" Filtered Tags : ");
                                    appendedTagString = true;
                                }

                                exemplarString.Append($"{result.Key}: {result.Value}");
                                exemplarString.Append(' ');
                            }
                        }

                        exemplarString.AppendLine();
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

                if (exemplarString.Length > 0)
                {
                    msg.AppendLine();
                    msg.AppendLine("Exemplars");
                    msg.Append(exemplarString.ToString());
                }

                this.WriteLine(msg.ToString());
            }
        }

        return ExportResult.Success;
    }
}
