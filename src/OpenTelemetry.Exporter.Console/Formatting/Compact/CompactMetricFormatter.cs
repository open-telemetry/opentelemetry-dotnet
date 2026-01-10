// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting.Compact;

/// <summary>
/// Simple console exporter for OpenTelemetry logs.
/// </summary>
/// <remarks>
/// Default format is:
/// "[EndTimestamp] 'METRIC' [MetricName] [Duration] [unit=Unit] [[Tag=value],..] [value=X|sum=A|count=X min=Y max=Z sum=A]".
/// </remarks>
internal sealed class CompactMetricFormatter : CompactFormatterBase<Metric>
{
    private const ConsoleColor MetricForeground = ConsoleColor.DarkBlue;
    private const ConsoleColor MetricBackground = ConsoleColor.Black;
    private const string MetricText = "METRIC";

    public CompactMetricFormatter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch, ConsoleFormatterContext context)
    {
        var console = this.Options.Console;

        foreach (var metric in batch)
        {
            foreach (var metricPoint in metric.GetMetricPoints())
            {
                var timestamp = string.Empty;
                if (!string.IsNullOrEmpty(this.Options.TimestampFormat))
                {
                    var timestampToFormat = this.Options.UseUtcTimestamp
                        ? metricPoint.EndTime.ToUniversalTime()
                        : metricPoint.EndTime.ToLocalTime();

                    timestamp = timestampToFormat.ToString(
                        this.Options.TimestampFormat!,
                        CultureInfo.InvariantCulture);
                }

                var metricDetails = string.Empty;

                metricDetails += $" [{metric.Name}]";

                var duration = metricPoint.EndTime - metricPoint.StartTime;
                metricDetails += $" {duration.TotalSeconds:N0}s";

                if (!string.IsNullOrEmpty(metric.Unit))
                {
                    metricDetails += $" unit={metric.Unit}";
                }

                foreach (var tag in metricPoint.Tags)
                {
                    metricDetails += $" {tag.Key}={tag.Value}";
                }

                var metricType = metric.MetricType;
                if (metricType == MetricType.Histogram || metricType == MetricType.ExponentialHistogram)
                {
                    var count = metricPoint.GetHistogramCount();
                    metricDetails += $" count={count}";

                    if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                    {
                        metricDetails += $" min={min} max={max}";
                    }

                    var sum = metricPoint.GetHistogramSum();
                    metricDetails += $" sum={sum}";
                }
                else if (metricType.IsDouble())
                {
                    if (metricType.IsSum())
                    {
                        metricDetails += $" sum={metricPoint.GetSumDouble()}";
                    }
                    else
                    {
                        metricDetails += $" value={metricPoint.GetGaugeLastValueDouble()}";
                    }
                }
                else if (metricType.IsLong())
                {
                    if (metricType.IsSum())
                    {
                        metricDetails += $" sum={metricPoint.GetSumLong()}";
                    }
                    else
                    {
                        metricDetails += $" value={metricPoint.GetGaugeLastValueLong()}";
                    }
                }

                lock (console.SyncRoot)
                {
                    console.Write(timestamp);
                    console.WriteColor(MetricText, MetricForeground, MetricBackground);
                    console.WriteLine(metricDetails);
                }
            }
        }

        return ExportResult.Success;
    }
}
