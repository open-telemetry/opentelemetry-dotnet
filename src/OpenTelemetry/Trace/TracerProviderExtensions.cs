// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains extension methods for the <see cref="TracerProvider"/> class.
/// </summary>
public static class TracerProviderExtensions
{
    /// <summary>
    /// Add a processor to the provider.
    /// </summary>
    /// <param name="provider"><see cref="TracerProvider"/>.</param>
    /// <param name="processor"><see cref="BaseProcessor{T}"/>.</param>
    /// <returns>The supplied <see cref="TracerProvider"/> instance for call chaining.</returns>
    public static TracerProvider AddProcessor(this TracerProvider provider, BaseProcessor<Activity> processor)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfNull(processor);

        if (provider is TracerProviderSdk tracerProviderSdk)
        {
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
            tracerProviderSdk.AddProcessor(processor);
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        }

        return provider;
    }

    /// <summary>
    /// Flushes all the processors registered under TracerProviderSdk, blocks the current thread
    /// until flush completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="provider">TracerProviderSdk instance on which ForceFlush will be called.</param>
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
    public static bool ForceFlush(this TracerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is TracerProviderSdk tracerProviderSdk)
        {
            try
            {
                return tracerProviderSdk.OnForceFlush(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderException(nameof(tracerProviderSdk.OnForceFlush), ex);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Attempts to shutdown the TracerProviderSdk, blocks the current thread until
    /// shutdown completed or timed out.
    /// </summary>
    /// <param name="provider">TracerProviderSdk instance on which Shutdown will be called.</param>
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
    public static bool Shutdown(this TracerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is TracerProviderSdk tracerProviderSdk)
        {
            if (Interlocked.Increment(ref tracerProviderSdk.ShutdownCount) > 1)
            {
                return false; // shutdown already called
            }

            try
            {
                return tracerProviderSdk.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderException(nameof(tracerProviderSdk.OnShutdown), ex);
                return false;
            }
        }

        return true;
    }
}
