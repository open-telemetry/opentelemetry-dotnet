// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Exporter of OpenTelemetry metrics to Prometheus.
/// </summary>
[ExportModes(ExportModes.Pull)]
internal sealed class PrometheusExporter : BaseExporter<Metric>, IPullMetricExporter
{
    private Func<int, bool> funcCollect;
    private Func<Batch<Metric>, ExportResult> funcExport;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="PrometheusExporterOptions"/>.</param>
    public PrometheusExporter(PrometheusExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        this.ScrapeResponseCacheDurationMilliseconds = options.ScrapeResponseCacheDurationMilliseconds;
        this.ScopeInfoEnabled = options.ScopeInfoEnabled;

        this.CollectionManager = new PrometheusCollectionManager(this);
    }

    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    public Func<int, bool> Collect
    {
        get => this.funcCollect;
        set => this.funcCollect = value;
    }

    internal Func<Batch<Metric>, ExportResult> OnExport
    {
        get => this.funcExport;
        set => this.funcExport = value;
    }

    internal Action OnDispose { get; set; }

    internal PrometheusCollectionManager CollectionManager { get; }

    internal int ScrapeResponseCacheDurationMilliseconds { get; }

    internal bool OpenMetricsRequested { get; set; }

    internal bool ScopeInfoEnabled { get; set; }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        return this.OnExport(metrics);
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
