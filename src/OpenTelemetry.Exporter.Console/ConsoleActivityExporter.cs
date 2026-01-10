// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Formatting;
using OpenTelemetry.Exporter.Formatting.Detail;

namespace OpenTelemetry.Exporter;

public class ConsoleActivityExporter : ConsoleExporter<Activity>
{
    public ConsoleActivityExporter(ConsoleExporterOptions options)
        : base(options)
    {
        this.ConsoleFormatter = new DetailActivityFormatter(options);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        var context = new ConsoleFormatterContext() { GetResource = this.ParentProvider.GetResource };
        return this.ConsoleFormatter!.Export(batch, context);
    }
}
