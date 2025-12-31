// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Formatting;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter;

public class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
{
    public ConsoleLogRecordExporter(ConsoleExporterOptions options)
        : base(options)
    {
        this.ConsoleFormatter = FormatterFactory
            .GetFormatterFactory(options.Formatter)
            .GetLogRecordFormatter(options);
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var context = new ConsoleFormatterContext() { GetResource = this.ParentProvider.GetResource };
        return this.ConsoleFormatter!.Export(batch, context);
    }

    protected override void Dispose(bool disposing)
    {
        this.ConsoleFormatter?.Dispose();
        base.Dispose(disposing);
    }
}
