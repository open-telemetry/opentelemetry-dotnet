// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Formatting;
using OpenTelemetry.Exporter.Formatting.Detail;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

public class ConsoleMetricExporter : ConsoleExporter<Metric>
{
    public ConsoleMetricExporter(ConsoleExporterOptions options)
        : base(options)
    {
        this.ConsoleFormatter = new DetailMetricFormatter(options);
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        var context = new ConsoleFormatterContext() { GetResource = this.ParentProvider.GetResource };
        return this.ConsoleFormatter!.Export(batch, context);
    }
}
