// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

internal class SimpleMultithreadedExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleMultithreadedExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    public SimpleMultithreadedExportProcessor(BaseExporter<T> exporter)
        : base(exporter)
    {
    }

    /// <inheritdoc />
    protected override void OnExport(T data)
    {
        try
        {
            this.exporter.Export(new Batch<T>(data));
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.OnExport), ex);
        }
    }
}
