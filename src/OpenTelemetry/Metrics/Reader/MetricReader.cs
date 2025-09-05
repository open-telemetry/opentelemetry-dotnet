// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader base class.
/// </summary>
public abstract partial class MetricReader : IDisposable
{
    private const MetricReaderTemporalityPreference MetricReaderTemporalityPreferenceUnspecified = (MetricReaderTemporalityPreference)0;

    private static readonly Func<Type, AggregationTemporality> CumulativeTemporalityPreferenceFunc =
        (instrumentType) => AggregationTemporality.Cumulative;

    private static readonly Func<Type, AggregationTemporality> MonotonicDeltaTemporalityPreferenceFunc = (instrumentType) =>
    {
        return instrumentType.GetGenericTypeDefinition() switch
        {
            var type when type == typeof(Counter<>) => AggregationTemporality.Delta,
            var type when type == typeof(ObservableCounter<>) => AggregationTemporality.Delta,
            var type when type == typeof(Histogram<>) => AggregationTemporality.Delta,

            // Temporality is not defined for gauges, so this does not really affect anything.
            var type when type == typeof(ObservableGauge<>) => AggregationTemporality.Delta,
            var type when type == typeof(Gauge<>) => AggregationTemporality.Delta,

            var type when type == typeof(UpDownCounter<>) => AggregationTemporality.Cumulative,
            var type when type == typeof(ObservableUpDownCounter<>) => AggregationTemporality.Cumulative,

            // TODO: Consider logging here because we should not fall through to this case.
            _ => AggregationTemporality.Delta,
        };
    };

    private readonly Lock newTaskLock = new();
    private readonly Lock onCollectLock = new();
    private readonly TaskCompletionSource<bool> shutdownTcs = new();
    private MetricReaderTemporalityPreference temporalityPreference = MetricReaderTemporalityPreferenceUnspecified;
    private Func<Type, AggregationTemporality> temporalityFunc = CumulativeTemporalityPreferenceFunc;
    private int shutdownCount;
    private TaskCompletionSource<bool>? collectionTcs;
    private BaseProvider? parentProvider;

    /// <summary>
    /// Gets or sets the metric reader temporality preference.
    /// </summary>
    public MetricReaderTemporalityPreference TemporalityPreference
    {
        get
        {
            if (this.temporalityPreference == MetricReaderTemporalityPreferenceUnspecified)
            {
                this.temporalityPreference = MetricReaderTemporalityPreference.Cumulative;
            }

            return this.temporalityPreference;
        }

        set
        {
            if (this.temporalityPreference != MetricReaderTemporalityPreferenceUnspecified)
            {
                throw new NotSupportedException($"The temporality preference cannot be modified (the current value is {this.temporalityPreference}).");
            }

            this.temporalityPreference = value;
            this.temporalityFunc = value switch
            {
                MetricReaderTemporalityPreference.Delta => MonotonicDeltaTemporalityPreferenceFunc,
                _ => CumulativeTemporalityPreferenceFunc,
            };
        }
    }

    /// <summary>
    /// Attempts to collect the metrics, blocks the current thread until
    /// metrics collection completed, shutdown signaled or timed out.
    /// If there are asynchronous instruments involved, their callback
    /// functions will be triggered.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when metrics collection succeeded; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
    /// </exception>
    /// <remarks>
    /// This function guarantees thread-safety. If multiple calls occurred
    /// simultaneously, they might get folded and result in less calls to
    /// the <c>OnCollect</c> callback for improved performance, as long as
    /// the semantic can be preserved.
    /// </remarks>
    public bool Collect(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect method called.");
        var shouldRunCollect = false;
        var tcs = this.collectionTcs;

        if (tcs == null)
        {
            lock (this.newTaskLock)
            {
                tcs = this.collectionTcs;

                if (tcs == null)
                {
                    shouldRunCollect = true;
                    tcs = new TaskCompletionSource<bool>();
                    this.collectionTcs = tcs;
                }
            }
        }

        if (!shouldRunCollect)
        {
            return Task.WaitAny([tcs.Task, this.shutdownTcs.Task], timeoutMilliseconds) == 0 && tcs.Task.Result;
        }

        var result = false;
        try
        {
            lock (this.onCollectLock)
            {
                this.collectionTcs = null;
                result = this.OnCollect(timeoutMilliseconds);
            }
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Collect), ex);
        }

        tcs.TrySetResult(result);

        if (result)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect succeeded.");
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Collect failed.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to shutdown the processor, blocks the current thread until
    /// shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
    /// </exception>
    /// <remarks>
    /// This function guarantees thread-safety. Only the first call will
    /// win, subsequent calls will be no-op.
    /// </remarks>
    public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown called.");

        if (Interlocked.CompareExchange(ref this.shutdownCount, 1, 0) != 0)
        {
            return false; // shutdown already called
        }

        var result = false;
        try
        {
            result = this.OnShutdown(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderException(nameof(this.Shutdown), ex);
        }

        this.shutdownTcs.TrySetResult(result);

        if (result)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown succeeded.");
        }
        else
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.Shutdown failed.");
        }

        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal virtual void SetParentProvider(BaseProvider parentProvider)
    {
        if (this.parentProvider != null && this.parentProvider != parentProvider)
        {
            throw new NotSupportedException("A MetricReader must not be registered with multiple MeterProviders.");
        }

        this.parentProvider = parentProvider;
    }

    /// <summary>
    /// Processes a batch of metrics.
    /// </summary>
    /// <param name="metrics">Batch of metrics to be processed.</param>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when metrics processing succeeded; otherwise,
    /// <c>false</c>.
    /// </returns>
    internal virtual bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        return true;
    }

    /// <summary>
    /// Called by <c>Collect</c>. This function should block the current
    /// thread until metrics collection completed, shutdown signaled or
    /// timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when metrics collection succeeded; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the threads which called
    /// <c>Collect</c>. This function should not throw exceptions.
    /// </remarks>
    protected virtual bool OnCollect(int timeoutMilliseconds)
    {
        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("MetricReader.OnCollect called.");

        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        var meterProviderSdk = this.parentProvider as MeterProviderSdk;
        meterProviderSdk?.CollectObservableInstruments();

        OpenTelemetrySdkEventSource.Log.MetricReaderEvent("Observable instruments collected.");

        var metrics = this.GetMetricsBatch();

        bool result;
        if (sw == null)
        {
            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics called.");
            result = this.ProcessMetrics(metrics, Timeout.Infinite);
            if (result)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics succeeded.");
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics failed.");
            }

            return result;
        }
        else
        {
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

            if (timeout <= 0)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("OnCollect failed timeout period has elapsed.");
                return false;
            }

            OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics called.");
            result = this.ProcessMetrics(metrics, (int)timeout);
            if (result)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics succeeded.");
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("ProcessMetrics failed.");
            }

            return result;
        }
    }

    /// <summary>
    /// Called by <c>Shutdown</c>. This function should block the current
    /// thread until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the thread which made the
    /// first call to <c>Shutdown</c>. This function should not throw
    /// exceptions.
    /// </remarks>
    protected virtual bool OnShutdown(int timeoutMilliseconds)
    {
        return this.Collect(timeoutMilliseconds);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally
    /// releases the managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
