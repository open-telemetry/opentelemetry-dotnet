// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
    private Func<int, bool>? funcCollect;
    private ExportFunc? funcExport;
    private Resource? resource;
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

        this.CollectionManager = new PrometheusCollectionManager(this);
    }

    public delegate ExportResult ExportFunc(in Batch<Metric> batch);

    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    public Func<int, bool>? Collect
    {
        get => this.funcCollect;
        set => this.funcCollect = value;
    }

    internal ExportFunc? OnExport
    {
        get => this.funcExport;
        set => this.funcExport = value;
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
        Debug.Assert(this.OnExport != null, "this.OnExport was null");

        return this.OnExport!(in metrics);
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
