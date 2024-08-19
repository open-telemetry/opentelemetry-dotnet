// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Exporter of OpenTelemetry metrics to Prometheus.
/// </summary>
[ExportModes(ExportModes.Pull)]
internal sealed class PrometheusExporter : BaseExporter<Metric>, IPullMetricExporter
{
    private Func<int, bool>? funcCollect;
    private Func<Batch<Metric>, ExportResult>? funcExport;
    private Resource? resource;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="PrometheusExporterOptions"/>.</param>
    public PrometheusExporter(PrometheusExporterOptions options)
    {
        this.ScrapeResponseCacheDurationMilliseconds = options.ScrapeResponseCacheDurationMilliseconds;
        this.DisableTotalNameSuffixForCounters = options.DisableTotalNameSuffixForCounters;

        this.CollectionManager = new PrometheusCollectionManager(this);

        this.funcCollect = _ => true;
        this.funcExport = _ => ExportResult.Success;
        this.resource = null;
    }

    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    public Func<int, bool>? Collect
    {
        get => this.funcCollect ?? null;
        set => this.funcCollect = value ?? null;
    }

    internal Func<Batch<Metric>, ExportResult>? OnExport
    {
        get => this.funcExport ?? null;
        set => this.funcExport = value ?? null;
    }

    internal Action? OnDispose { get; set; }

    internal PrometheusCollectionManager CollectionManager { get; }

    internal int ScrapeResponseCacheDurationMilliseconds { get; }

    internal bool DisableTotalNameSuffixForCounters { get; }

    internal bool OpenMetricsRequested { get; set; }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        return this.OnExport?.Invoke(metrics) ?? ExportResult.Failure;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.OnDispose?.Invoke();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
