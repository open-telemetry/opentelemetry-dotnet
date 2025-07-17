// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

public class ConsoleMetricExporter : ConsoleExporter<Metric>
{
    public ConsoleMetricExporter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            var msg = new StringBuilder(Environment.NewLine);
#if NET
            msg.Append(CultureInfo.InvariantCulture, $"Metric Name: {metric.Name}");
#else
            msg.Append($"Metric Name: {metric.Name}");
#endif
            if (string.IsNullOrEmpty(metric.Description))
            {
#if NET
                msg.Append(CultureInfo.InvariantCulture, $", Description: {metric.Description}");
#else
                msg.Append($", Description: {metric.Description}");
#endif
            }

            if (string.IsNullOrEmpty(metric.Unit))
            {
#if NET
                msg.Append(CultureInfo.InvariantCulture, $", Unit: {metric.Unit}");
#else
                msg.Append($", Unit: {metric.Unit}");
#endif
            }

            this.WriteLine(msg.ToString());

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                string valueDisplay = string.Empty;
                StringBuilder tagsBuilder = new StringBuilder();
                foreach (var tag in metricPoint.Tags)
                {
                    if (this.TagWriter.TryTransformTag(tag, out var result))
                    {
#if NET
                        tagsBuilder.Append(CultureInfo.InvariantCulture, $"{result.Key}: {result.Value}");
#else
                        tagsBuilder.Append($"{result.Key}: {result.Value}");
#endif
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
#if NET
                    bucketsBuilder.Append(CultureInfo.InvariantCulture, $"Sum: {sum} Count: {count} ");
#else
                    bucketsBuilder.Append($"Sum: {sum} Count: {count} ");
#endif
                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
#if NET
                        bucketsBuilder.Append(CultureInfo.InvariantCulture, $"Min: {min} Max: {max} ");
#else
                        bucketsBuilder.Append($"Min: {min} Max: {max} ");
#endif
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
#if NET
                            bucketsBuilder.AppendLine(CultureInfo.InvariantCulture, $"Zero Bucket:{exponentialHistogramData.ZeroCount}");
#else
                            bucketsBuilder.AppendLine($"Zero Bucket:{exponentialHistogramData.ZeroCount}");
#endif
                        }

                        var offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            var lowerBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(offset, scale).ToString(CultureInfo.InvariantCulture);
                            var upperBound = Base2ExponentialBucketHistogramHelper.CalculateLowerBoundary(++offset, scale).ToString(CultureInfo.InvariantCulture);
#if NET
                            bucketsBuilder.AppendLine(CultureInfo.InvariantCulture, $"({lowerBound}, {upperBound}]:{bucketCount}");
#else
                            bucketsBuilder.AppendLine($"({lowerBound}, {upperBound}]:{bucketCount}");
#endif
                        }
                    }

                    valueDisplay = bucketsBuilder.ToString();
                }
                else if (metricType.IsDouble())
                {
                    valueDisplay = metricType.IsSum() ? metricPoint.GetSumDouble().ToString(CultureInfo.InvariantCulture) : metricPoint.GetGaugeLastValueDouble().ToString(CultureInfo.InvariantCulture);
                }
                else if (metricType.IsLong())
                {
                    valueDisplay = metricType.IsSum() ? metricPoint.GetSumLong().ToString(CultureInfo.InvariantCulture) : metricPoint.GetGaugeLastValueLong().ToString(CultureInfo.InvariantCulture);
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
                                    exemplarString.Append(" Filtered Tags: ");
                                    appendedTagString = true;
                                }

#if NET
                                exemplarString.Append(CultureInfo.InvariantCulture, $"{result.Key}: {result.Value}");
#else
                                exemplarString.Append($"{result.Key}: {result.Value}");
#endif
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
                if (string.IsNullOrEmpty(tags))
                {
                    msg.Append(' ');
                }

                msg.AppendLine();
                msg.AppendLine(CultureInfo.InvariantCulture, $"Metric Type: {metric.MetricType}");
                msg.AppendLine();
#if NET
                msg.Append(CultureInfo.InvariantCulture, $"Value: {valueDisplay}");
#else
                msg.Append($"Value: {valueDisplay}");
#endif

                if (exemplarString.Length > 0)
                {
                    msg.AppendLine();
                    msg.AppendLine("Exemplars");
                    msg.Append(exemplarString);
                }

                this.WriteLine(msg.ToString());

                this.WriteLine("Instrumentation scope (Meter):");
                this.WriteLine($"\tName: {metric.MeterName}");
                if (!string.IsNullOrEmpty(metric.MeterVersion))
                {
                    this.WriteLine($"\tVersion: {metric.MeterVersion}");
                }

                if (metric.MeterTags?.Any() == true)
                {
                    this.WriteLine("\tTags:");
                    foreach (var meterTag in metric.MeterTags)
                    {
                        if (this.TagWriter.TryTransformTag(meterTag, out var result))
                        {
                            this.WriteLine($"\t\t{result.Key}: {result.Value}");
                        }
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    this.WriteLine("Resource associated with Metric:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        if (this.TagWriter.TryTransformTag(resourceAttribute.Key, resourceAttribute.Value, out var result))
                        {
                            this.WriteLine($"\t{result.Key}: {result.Value}");
                        }
                    }
                }
            }
        }

        return ExportResult.Success;
    }
}
