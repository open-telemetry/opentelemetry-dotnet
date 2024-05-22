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
    private readonly object syncObject;
    private readonly ConcurrencyModes supportedConcurrencyModes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    protected SimpleExportProcessor(BaseExporter<T> exporter)
        : base(exporter)
    {
        var exporterType = exporter.GetType();
        var attributes = exporterType.GetCustomAttributes(typeof(ConcurrencyModesAttribute), true);
        if (attributes.Length > 0)
        {
            var attr = (ConcurrencyModesAttribute)attributes[attributes.Length - 1];
            this.supportedConcurrencyModes = attr.Supported;
        }

        if (!this.supportedConcurrencyModes.HasFlag(ConcurrencyModes.Multithreaded))
        {
            this.syncObject = new object();
        }
    }

    /// <inheritdoc />
    protected override void OnExport(T data)
    {
        if (this.syncObject is null)
        {
            this.OnExportInternal(data);
        }
        else
        {
            lock (this.syncObject)
            {
                this.OnExportInternal(data);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnExportInternal(T data)
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
