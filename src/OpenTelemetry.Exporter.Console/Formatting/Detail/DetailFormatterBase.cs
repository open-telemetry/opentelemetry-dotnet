// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting.Detail;

internal abstract class DetailFormatterBase<T> : IConsoleFormatter<T>
    where T : class
{
    private readonly ConsoleExporterOptions options;

    protected DetailFormatterBase(ConsoleExporterOptions options)
    {
        this.options = options ?? new ConsoleExporterOptions();
        this.TagWriter = new ConsoleTagWriter(this.OnUnsupportedTagDropped);
    }

    internal ConsoleTagWriter TagWriter { get; }

    public abstract ExportResult Export(in Batch<T> batch, ConsoleFormatterContext context);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void WriteLine(string message)
    {
        if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Console))
        {
            this.options.Console.WriteLine(message);
        }

        if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug))
        {
            System.Diagnostics.Trace.WriteLine(message);
        }
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

    private void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName)
    {
        this.WriteLine(
            $"Unsupported attribute value type '{tagValueTypeFullName}' for '{tagKey}'.");
    }
}
