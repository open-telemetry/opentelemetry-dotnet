// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Exporter of OpenTelemetry metrics to Prometheus.
/// </summary>
[ExportModes(ExportModes.Pull)]
internal sealed class PrometheusExporter : BaseExporter<Metric>, IPullMetricExporter
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="PrometheusExporterOptions"/>.</param>
    public PrometheusExporter(PrometheusExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        this.ScrapeResponseCacheDurationMilliseconds = options.ScrapeResponseCacheDurationMilliseconds;
        this.DisableTotalNameSuffixForCounters = options.DisableTotalNameSuffixForCounters;
        this.DisableTimestamp = options.DisableTimestamp;

        this.CollectionManager = new PrometheusCollectionManager(this);
    }

    public delegate ExportResult ExportFunc(in Batch<Metric> batch);

    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    public Func<int, bool>? Collect { get; set; }

    internal ExportFunc? OnExport { get; set; }

    internal Action? OnDispose { get; set; }

    internal PrometheusCollectionManager CollectionManager { get; }

    internal int ScrapeResponseCacheDurationMilliseconds { get; }

    internal bool DisableTotalNameSuffixForCounters { get; }

    internal bool OpenMetricsRequested { get; set; }

    internal bool DisableTimestamp { get; set; }

    internal Resource Resource
    {
        get => field ??= this.ParentProvider.GetResource();
        private set;
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> metrics)
        => this.OnExport!(in metrics);

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
