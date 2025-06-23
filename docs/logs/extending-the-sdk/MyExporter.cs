// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Globalization;
#endif
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Logs;

internal sealed class MyExporter : BaseExporter<LogRecord>
{
    private readonly string name;

    public MyExporter(string name = "MyExporter")
    {
        this.name = name;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        // SuppressInstrumentationScope should be used to prevent exporter
        // code from generating telemetry and causing live-loop.
        using var scope = SuppressInstrumentationScope.Begin();

        var sb = new StringBuilder();
        foreach (var record in batch)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

#if NET
            sb.Append(CultureInfo.InvariantCulture, $"{record}(");
#else
            sb.Append($"{record}(");
#endif

            int scopeDepth = -1;

            record.ForEachScope(ProcessScope, sb);

            void ProcessScope(LogRecordScope scope, StringBuilder builder)
            {
                if (++scopeDepth > 0)
                {
                    builder.Append(", ");
                }

#if NET
                builder.Append(CultureInfo.InvariantCulture, $"{scope.Scope}");
#else
                builder.Append($"{scope.Scope}");
#endif
            }

            sb.Append(')');
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
