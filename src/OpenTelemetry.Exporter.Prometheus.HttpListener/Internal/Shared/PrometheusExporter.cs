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
    /// <summary>
    /// The maximum number of bytes of a scrape response written to the HTTP server in a single write.
    /// </summary>
    internal const int MaximumResponseWriteSize = 64 * 1024;

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
    /// </summary>
    /// <param name="options"><see cref="PrometheusExporterOptions"/>.</param>
    public PrometheusExporter(PrometheusExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        this.ScopeInfoEnabled = options.ScopeInfoEnabled;
        this.ScrapeResponseCacheDurationMilliseconds = options.ScrapeResponseCacheDurationMilliseconds;
        this.TargetInfoEnabled = options.TargetInfoEnabled;
        this.DisableTotalNameSuffixForCounters = options.DisableTotalNameSuffixForCounters;
        this.TranslationStrategy = options.TranslationStrategy;
        this.ResourceConstantLabels = options.ResourceConstantLabels;
        this.MaxScrapeResponseSizeBytes = options.MaxScrapeResponseSizeBytes;
        this.ScrapeResponseTimeoutMilliseconds = options.ScrapeResponseTimeoutMilliseconds;

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

    internal bool ScopeInfoEnabled { get; }

    internal bool TargetInfoEnabled { get; }

    internal int ScrapeResponseCacheDurationMilliseconds { get; }

    internal bool DisableTotalNameSuffixForCounters { get; }

    internal PrometheusTranslationStrategy TranslationStrategy { get; }

    internal bool AppendSuffixes => this.TranslationStrategy.AppendSuffixes();

    internal Func<string, bool>? ResourceConstantLabels { get; }

    internal int MaxScrapeResponseSizeBytes { get; }

    internal int ScrapeResponseTimeoutMilliseconds { get; }

    internal Resource Resource
    {
        get
        {
            // Collection can run before the provider is attached. Do not cache its empty fallback.
            if (field == null && this.ParentProvider is { } parentProvider)
            {
                field = parentProvider.GetResource();
            }

            return field ?? Resource.Empty;
        }

        private set;
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
        => this.OnExport!(in batch);

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
