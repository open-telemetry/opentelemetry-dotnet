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
    private Func<int, bool> funcCollect;
    private Resource resource;
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

    internal event Func<Batch<Metric>, ExportResult> OnExport;

    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    public Func<int, bool> Collect
    {
        get => this.funcCollect;
        set => this.funcCollect = value;
    }

    internal Action OnDispose { get; set; }

    internal PrometheusCollectionManager CollectionManager { get; }

    internal int ScrapeResponseCacheDurationMilliseconds { get; }

    internal bool DisableTotalNameSuffixForCounters { get; }

    internal TaskCompletionSource<bool> CollectionCts { get; set; }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> metrics)
    {
        var result = ExportResult.Success;
        foreach (var onExport in this.OnExport.GetInvocationList())
        {
            if (((Func<Batch<Metric>, ExportResult>)onExport)(metrics) != ExportResult.Success)
            {
                result = ExportResult.Failure;
            }
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
                this.OnDispose?.Invoke();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
