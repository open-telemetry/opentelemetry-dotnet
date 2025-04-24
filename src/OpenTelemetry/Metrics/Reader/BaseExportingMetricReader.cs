// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader implementation which exports metrics to the configured
/// MetricExporter upon <see cref="MetricReader.Collect(int)"/>.
/// </summary>
public class BaseExportingMetricReader : MetricReader
{
    /// <summary>
    /// Gets the exporter used by the metric reader.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
    protected readonly BaseExporter<Metric> exporter;
#pragma warning restore CA1051 // Do not declare visible instance fields

    private readonly string exportCalledMessage;
    private readonly string exportSucceededMessage;
    private readonly string exportFailedMessage;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseExportingMetricReader"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    public BaseExportingMetricReader(BaseExporter<Metric> exporter)
    {
        Guard.ThrowIfNull(exporter);

        this.exporter = exporter;

        var exporterType = exporter.GetType();
        var attributes = exporterType.GetCustomAttributes(typeof(ExportModesAttribute), true);
        if (attributes.Length > 0)
        {
            var attr = (ExportModesAttribute)attributes[attributes.Length - 1];
            this.SupportedExportModes = attr.Supported;
        }

        if (exporter is IPullMetricExporter pullExporter)
        {
            if (this.SupportedExportModes.HasFlag(ExportModes.Push))
            {
                pullExporter.Collect = this.Collect;
            }
            else
            {
                pullExporter.Collect = (timeoutMilliseconds) =>
                {
                    using (PullMetricScope.Begin())
                    {
                        return this.Collect(timeoutMilliseconds);
                    }
                };
            }
        }

        this.exportCalledMessage = $"{nameof(BaseExportingMetricReader)} calling {this.Exporter}.{nameof(this.Exporter.Export)} method.";
        this.exportSucceededMessage = $"{this.Exporter}.{nameof(this.Exporter.Export)} succeeded.";
        this.exportFailedMessage = $"{this.Exporter}.{nameof(this.Exporter.Export)} failed.";
    }

    internal BaseExporter<Metric> Exporter => this.exporter;

    /// <summary>
    /// Gets the supported <see cref="ExportModes"/>.
    /// </summary>
    protected ExportModes SupportedExportModes { get; } = ExportModes.Push | ExportModes.Pull;

    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        base.SetParentProvider(parentProvider);
        this.exporter.ParentProvider = parentProvider;
    }

    /// <inheritdoc/>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        // TODO: Do we need to consider timeout here?
        try
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportCalledMessage);
            var result = this.exporter.Export(metrics);
            if (result == ExportResult.Success)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportSucceededMessage);
                return true;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent(this.exportFailedMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.ProcessMetrics), ex);
            return false;
        }
    }

    /// <inheritdoc />
    protected override bool OnCollect(int timeoutMilliseconds)
    {
        if (this.SupportedExportModes.HasFlag(ExportModes.Push))
        {
            return base.OnCollect(timeoutMilliseconds);
        }

        if (this.SupportedExportModes.HasFlag(ExportModes.Pull) && PullMetricScope.IsPullAllowed)
        {
            return base.OnCollect(timeoutMilliseconds);
        }

        // TODO: add some error log
        return false;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = true;

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            result = this.Collect(Timeout.Infinite) && result;
            result = this.exporter.Shutdown(Timeout.Infinite) && result;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            result = this.Collect(timeoutMilliseconds) && result;
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
            result = this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
        }

        return result;
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
                    if (this.exporter is IPullMetricExporter pullExporter)
                    {
                        pullExporter.Collect = null;
                    }

                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Dispose), ex);
                }
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
