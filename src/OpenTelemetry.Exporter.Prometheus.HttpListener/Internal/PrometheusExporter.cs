// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
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
    internal TaskCompletionSource<bool> CollectionTcs;

    private Func<int, bool> funcCollect;
    private Resource resource;
    private bool disposed;
    private int collectionLockState;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterCollectionLock()
    {
        SpinWait lockWait = default;
        while (true)
        {
            if (Interlocked.CompareExchange(ref this.collectionLockState, 1, this.collectionLockState) != 0)
            {
                lockWait.SpinOnce();
                continue;
            }

            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCollectionLock()
    {
        Interlocked.Exchange(ref this.collectionLockState, 0);
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
