// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Base processor base class.
/// </summary>
/// <typeparam name="T">The type of object to be processed.</typeparam>
#pragma warning disable CA1012 // Abstract types should not have public constructors
public abstract class BaseProcessor<T> : IDisposable
#pragma warning restore CA1012 // Abstract types should not have public constructors
{
    private readonly string typeName;
    private int shutdownCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseProcessor{T}"/> class.
    /// </summary>
    public BaseProcessor()
    {
        this.typeName = this.GetType().Name;
    }

    /// <summary>
    /// Gets the parent <see cref="BaseProvider"/>.
    /// </summary>
    public BaseProvider? ParentProvider { get; private set; }

    /// <summary>
    /// Gets or sets the weight of the processor when added to the provider
    /// pipeline. Default value: <c>0</c>.
    /// </summary>
    /// <remarks>
    /// Note: Weight is used to order processors when building a provider
    /// pipeline. Lower weighted processors come before higher weighted
    /// processors. Changing the weight after a pipeline has been constructed
    /// has no effect.
    /// </remarks>
    internal int PipelineWeight { get; set; }

    /// <summary>
    /// Called synchronously when a telemetry object is started.
    /// </summary>
    /// <param name="data">
    /// The started telemetry object.
    /// </param>
    /// <remarks>
    /// This function is called synchronously on the thread which started
    /// the telemetry object. This function should be thread-safe, and
    /// should not block indefinitely or throw exceptions.
    /// </remarks>
    public virtual void OnStart(T data)
    {
    }

    /// <summary>
    /// Called synchronously when a telemetry object is ended.
    /// </summary>
    /// <param name="data">
    /// The ended telemetry object.
    /// </param>
    /// <remarks>
    /// This function is called synchronously on the thread which ended
    /// the telemetry object. This function should be thread-safe, and
    /// should not block indefinitely or throw exceptions.
    /// </remarks>
    public virtual void OnEnd(T data)
    {
    }

    /// <summary>
    /// Flushes the processor, blocks the current thread until flush
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
            bool result = this.OnForceFlush(timeoutMilliseconds);

            OpenTelemetrySdkEventSource.Log.ProcessorForceFlushInvoked(this.typeName, result);

            return result;
        }
        catch (Exception ex)
        {
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ForceFlush), ex);
            return false;
        }
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

        if (Interlocked.CompareExchange(ref this.shutdownCount, 1, 0) != 0)
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

    /// <inheritdoc/>
    public override string ToString()
        => this.typeName;

    internal virtual void SetParentProvider(BaseProvider parentProvider)
    {
        this.ParentProvider = parentProvider;
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
