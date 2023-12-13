// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for the <see cref="LoggerProvider"/> class.
/// </summary>
#if EXPOSE_EXPERIMENTAL_FEATURES
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    static class LoggerProviderExtensions
{
#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Add a processor to the <see cref="LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></para>
    /// Note: The supplied <paramref name="processor"/> will be
    /// automatically disposed when then the <see
    /// cref="LoggerProvider"/> is disposed.
    /// </remarks>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
    /// <param name="processor">Log processor to add.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
#else
    /// <summary>
    /// Add a processor to the <see cref="LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The supplied <paramref name="processor"/> will be
    /// automatically disposed when then the <see
    /// cref="LoggerProvider"/> is disposed.
    /// </remarks>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
    /// <param name="processor">Log processor to add.</param>
    /// <returns>The supplied <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
#endif
    public static LoggerProvider AddProcessor(this LoggerProvider provider, BaseProcessor<LogRecord> processor)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfNull(processor);

        if (provider is LoggerProviderSdk loggerProviderSdk)
        {
            loggerProviderSdk.AddProcessor(processor);
        }

        return provider;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Flushes all the processors registered under <see cref="LoggerProvider"/>, blocks the current thread
    /// until flush completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
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
    /// <para><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></para>
    /// This function guarantees thread-safety.
    /// </remarks>
#else
    /// <summary>
    /// Flushes all the processors registered under <see cref="LoggerProvider"/>, blocks the current thread
    /// until flush completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which ForceFlush will be called.</param>
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
#endif
    public static bool ForceFlush(this LoggerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is LoggerProviderSdk loggerProviderSdk)
        {
            return loggerProviderSdk.ForceFlush(timeoutMilliseconds);
        }

        return true;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Attempts to shutdown the <see cref="LoggerProvider"/>, blocks the current thread until
    /// shutdown completed or timed out.
    /// </summary>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which Shutdown will be called.</param>
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
    /// <para><inheritdoc cref="Sdk.CreateLoggerProviderBuilder" path="/remarks"/></para>
    /// This function guarantees thread-safety. Only the first call will
    /// win, subsequent calls will be no-op.
    /// </remarks>
#else
    /// <summary>
    /// Attempts to shutdown the <see cref="LoggerProvider"/>, blocks the current thread until
    /// shutdown completed or timed out.
    /// </summary>
    /// <param name="provider"><see cref="LoggerProvider"/> instance on which Shutdown will be called.</param>
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
#endif
    public static bool Shutdown(this LoggerProvider provider, int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfNull(provider);
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        if (provider is LoggerProviderSdk loggerProviderSdk)
        {
            return loggerProviderSdk.Shutdown(timeoutMilliseconds);
        }

        return true;
    }
}
