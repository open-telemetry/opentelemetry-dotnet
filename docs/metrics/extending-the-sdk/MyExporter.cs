// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Globalization;
#endif
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Metrics;

internal sealed class MyExporter : BaseExporter<Metric>
{
    private readonly string name;

    public MyExporter(string name = "MyExporter")
    {
        this.name = name;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        // SuppressInstrumentationScope should be used to prevent exporter
        // code from generating telemetry and causing live-loop.
        using var scope = SuppressInstrumentationScope.Begin();

        var sb = new StringBuilder();
        foreach (var metric in batch)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(metric.Name);

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
#if NET
                sb.Append(CultureInfo.InvariantCulture, $"{metricPoint.StartTime}");
#else
                sb.Append($"{metricPoint.StartTime}");
#endif
                foreach (var metricPointTag in metricPoint.Tags)
                {
#if NET
                    sb.Append(CultureInfo.InvariantCulture, $"{metricPointTag.Key} {metricPointTag.Value}");
#else
                    sb.Append($"{metricPointTag.Key} {metricPointTag.Value}");
#endif
                }
            }
        }

        Console.WriteLine($"{this.name}.Export([{sb}])");
        return ExportResult.Success;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Console.WriteLine($"{this.name}.OnShutdown(timeoutMilliseconds={timeoutMilliseconds})");
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
