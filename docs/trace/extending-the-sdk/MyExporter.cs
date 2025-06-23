// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry;

internal sealed class MyExporter : BaseExporter<Activity>
{
    private readonly string name;

    public MyExporter(string name = "MyExporter")
    {
        this.name = name;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        // SuppressInstrumentationScope should be used to prevent exporter
        // code from generating telemetry and causing live-loop.
        using var scope = SuppressInstrumentationScope.Begin();

        var sb = new StringBuilder();
        foreach (var activity in batch)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(activity.DisplayName);
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
        Console.WriteLine($"{this.name}.Dispose({disposing})");
    }
}
