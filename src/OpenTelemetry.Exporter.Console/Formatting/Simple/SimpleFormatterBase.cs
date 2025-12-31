// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting.Simple;

internal abstract class SimpleFormatterBase<T> : IConsoleFormatter<T>
    where T : class
{
    private readonly ConsoleExporterOptions exporterOptions;
    private readonly SimpleFormatterOptions formatterOptions;

    protected SimpleFormatterBase(ConsoleExporterOptions exporterOptions, SimpleFormatterOptions formatterOptions)
    {
        this.exporterOptions = exporterOptions;
        this.formatterOptions = formatterOptions;
    }

    public abstract ExportResult Export(in Batch<T> batch, ConsoleFormatterContext context);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally
    /// releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
