// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains extension methods for the <see cref="MeterProvider"/> class.
/// </summary>
public static class MeterProviderExtensions
{
    /// <summary>
    /// Flushes all the readers registered under MeterProviderSdk, blocks the current thread
    /// until flush completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="provider">MeterProviderSdk instance on which ForceFlush will be called.</param>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when force flush succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
    /// </exception>
    /// <remarks>
    /// This function guarantees thread-safety.
    /// </remarks>
    public static bool ForceFlush(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is MeterProviderSdk meterProviderSdk)
        {
            try
            {
                return meterProviderSdk.OnForceFlush(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnForceFlush), ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Attempts to shutdown the MeterProviderSdk, blocks the current thread until
    /// shutdown completed or timed out.
    /// </summary>
    /// <param name="provider">MeterProviderSdk instance on which Shutdown will be called.</param>
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
    public static bool Shutdown(this MeterProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is MeterProviderSdk meterProviderSdk)
        {
            if (Interlocked.Increment(ref meterProviderSdk.ShutdownCount) > 1)
            {
                return false; // shutdown already called
            }

            try
            {
                return meterProviderSdk.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.MeterProviderException(nameof(meterProviderSdk.OnShutdown), ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the Metric exporter of the given type from the provider.
    /// </summary>
    /// <typeparam name="T">The type of the Exporter.</typeparam>
    /// <param name="provider">The MeterProvider from which Exporter should be found.</param>
    /// <param name="exporter">The exporter instance.</param>
    /// <returns>true if the exporter of specified Type is found; otherwise false.</returns>
    internal static bool TryFindExporter<T>(
        this MeterProvider provider,
        [NotNullWhen(true)]
        out T? exporter)
        where T : BaseExporter<Metric>
    {
        if (provider is MeterProviderSdk meterProviderSdk)
        {
            return TryFindExporter(meterProviderSdk.Reader, out exporter);
        }

        exporter = null;
        return false;

        static bool TryFindExporter(MetricReader? reader, out T? exporter)
        {
            if (reader is BaseExportingMetricReader exportingMetricReader)
            {
                exporter = exportingMetricReader.Exporter as T;
                return exporter != null;
            }

            if (reader is CompositeMetricReader compositeMetricReader)
            {
                foreach (MetricReader childReader in compositeMetricReader)
                {
                    if (TryFindExporter(childReader, out exporter))
                    {
                        return true;
                    }
                }
            }

            exporter = null;
            return false;
        }
    }
}