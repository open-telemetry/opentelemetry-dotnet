// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that exports telemetry data at each OnEnd call.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
public abstract class SimpleExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    private readonly object syncObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    protected SimpleExportProcessor(BaseExporter<T> exporter)
        : base(exporter)
    {
    }

    /// <inheritdoc />
    protected override void OnExport(T data)
    {
        lock (this.syncObject)
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
}