// <copyright file="BaseExportProcessor.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

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
    protected readonly BaseExporter<T> exporter;
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

    public override void OnEnd(T data)
    {
        this.OnExport(data);
    }

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);

        this.exporter.ParentProvider = parentProvider;
    }

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
