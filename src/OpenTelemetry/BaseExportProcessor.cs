// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Type of Export Processor to be used.
/// </summary>
public enum ExportProcessorType
{
    /// <summary>
    /// Use SimpleExportProcessor.
    /// Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor">
    /// specification</a> for more information.
    /// </summary>
    Simple,

    /// <summary>
    /// Use BatchExportProcessor.
    /// Refer to <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#batching-processor">
    /// specification</a> for more information.
    /// </summary>
    Batch,
}

/// <summary>
/// Implements processor that exports telemetry objects.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
public abstract class BaseExportProcessor<T> : BaseProcessor<T>
    where T : class
{
    /// <summary>
    /// Gets the exporter used by the processor.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
    protected readonly BaseExporter<T> exporter;
#pragma warning restore CA1051 // Do not declare visible instance fields

    private readonly string friendlyTypeName;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    protected BaseExportProcessor(BaseExporter<T> exporter)
    {
        Guard.ThrowIfNull(exporter);

        this.friendlyTypeName = $"{this.GetType().Name}{{{exporter.GetType().Name}}}";
        this.exporter = exporter;
    }

    internal BaseExporter<T> Exporter => this.exporter;

    /// <inheritdoc />
    public override string ToString()
        => this.friendlyTypeName;

    /// <inheritdoc />
    public sealed override void OnStart(T data)
    {
    }

    /// <inheritdoc />
    public override void OnEnd(T data)
    {
        this.OnExport(data);
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        this.exporter.ParentProvider = parentProvider;
    }

    /// <summary>
    /// Called synchronously when a telemetry object is exported.
    /// </summary>
    /// <param name="data">
    /// The exported telemetry object.
    /// </param>
    /// <remarks>
    /// This function is called synchronously on the thread which ended
    /// the telemetry object. This function should be thread-safe, and
    /// should not block indefinitely or throw exceptions.
    /// </remarks>
    protected abstract void OnExport(T data);

    /// <inheritdoc />
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        return this.exporter.ForceFlush(timeoutMilliseconds);
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.exporter.Shutdown(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
