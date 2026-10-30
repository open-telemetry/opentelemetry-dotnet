// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

public class InMemoryExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly ICollection<T>? exportedItems;
    private readonly ExportFunc onExport;
    private bool disposed;
    private string? disposedStackTrace;

    public InMemoryExporter(ICollection<T> exportedItems)
    {
        this.exportedItems = exportedItems;
        this.onExport = this.DefaultExport;
    }

    internal InMemoryExporter(ExportFunc exportFunc)
    {
        this.onExport = exportFunc;
    }

    internal delegate ExportResult ExportFunc(in Batch<T> batch);

    public override ExportResult Export(in Batch<T> batch)
    {
        if (this.disposed)
        {
            // Note: In-memory exporter is designed for testing purposes so this error is strategic to surface lifecycle management bugs during development.
            throw new ObjectDisposedException(
                this.GetType().Name,
                $"The in-memory exporter is still being invoked after it has been disposed. This could be the result of invalid lifecycle management of the OpenTelemetry .NET SDK. Dispose was called on the following stack trace:{Environment.NewLine}{this.disposedStackTrace}");
        }

        return this.onExport(batch);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            this.disposedStackTrace = Environment.StackTrace;
            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private ExportResult DefaultExport(in Batch<T> batch)
    {
        if (this.exportedItems == null)
        {
            return ExportResult.Failure;
        }

        foreach (var data in batch)
        {
            // A metric whose metric points are split between export batches is
            // only added for the first of them. Once the batches have been
            // exported, the Metric exposes all of its metric points again, so
            // adding it for each batch would duplicate them.
            if (typeof(T) == typeof(Metric) && ((Metric)(object)data).AggregatorStore.ExportWindowStart > 0)
            {
                continue;
            }

            this.exportedItems.Add(data);
        }

        return ExportResult.Success;
    }
}
