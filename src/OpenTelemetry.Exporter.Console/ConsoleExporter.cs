// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Formatting;

namespace OpenTelemetry.Exporter;

public abstract class ConsoleExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly ConsoleExporterOptions options;

    protected ConsoleExporter(ConsoleExporterOptions options)
    {
        this.options = options ?? new ConsoleExporterOptions();
    }

    internal IConsoleFormatter<T>? ConsoleFormatter { get; set; }

    [Obsolete("Use console formatter instead")]
    protected void WriteLine(string message)
    {
        // Do nothing
    }
}
