// <copyright file="BaseExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Enumeration used to define the result of an export operation.
/// </summary>
public enum ExportResult
{
    /// <summary>
    /// Export succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Export failed.
    /// </summary>
    Failure = 1,
}

/// <summary>
/// Exporter base class.
/// </summary>
/// <typeparam name="T">The type of object to be exported.</typeparam>
public abstract class BaseExporter<T> : IDisposable
    where T : class
{
    private int shutdownCount;

    /// <summary>
    /// Gets the parent <see cref="BaseProvider"/>.
    /// </summary>
    public BaseProvider? ParentProvider { get; internal set; }

    /// <summary>
    /// Exports a batch of telemetry objects.
    /// </summary>
    /// <param name="batch">Batch of telemetry objects to export.</param>
    /// <returns>Result of the export operation.</returns>
    public abstract ExportResult Export(in Batch<T> batch);

    /// <summary>
    /// Flushes the exporter, blocks the current thread until flush
    /// completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
    /// </exception>
    /// <remarks>
    /// This function guarantees thread-safety.
    /// </remarks>
    public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        try
        {
            return this.OnForceFlush(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ForceFlush), ex);
            return false;
        }
    }

    /// <summary>
    /// Attempts to shutdown the exporter, blocks the current thread until
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

        if (Interlocked.Increment(ref this.shutdownCount) > 1)
        {
            return false; // shutdown already called
        }

        try
        {
            return this.OnShutdown(timeoutMilliseconds);
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Called by <c>ForceFlush</c>. This function should block the current
    /// thread until flush completed, shutdown signaled or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function is called synchronously on the thread which called
    /// <c>ForceFlush</c>. This function should be thread-safe, and should
    /// not throw exceptions.
    /// </remarks>
    protected virtual bool OnForceFlush(int timeoutMilliseconds)
    {
        return true;
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
        return true;
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
