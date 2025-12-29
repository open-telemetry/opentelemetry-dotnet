// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Formatting;
using OpenTelemetry.Exporter.Formatting.KeyValue;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter;

public class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
{
    public ConsoleLogRecordExporter(ConsoleExporterOptions options)
        : base(options)
    {
        Console.WriteLine("ConsoleLogRecordExporter ctor");
        this.ConsoleFormatter = new KeyValueLogRecordFormatter(options);
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        Console.WriteLine("ConsoleLogRecordExporter.Export()");
        var context = new ConsoleFormatterContext() { GetResource = this.ParentProvider.GetResource };
        return this.ConsoleFormatter!.Export(batch, context);
    }

    protected override void Dispose(bool disposing)
    {
        this.ConsoleFormatter?.Dispose();
        base.Dispose(disposing);
    }
}
