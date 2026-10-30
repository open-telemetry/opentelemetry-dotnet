// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Controls the number of samples of traces collected and sent to the backend.
/// </summary>
public abstract class Sampler
{
    private readonly Lock shutdownLock = new();
    private ShutdownState shutdownState;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sampler"/> class.
    /// </summary>
    protected Sampler()
    {
        this.Description = this.GetType().Name;
    }

    private enum ShutdownState
    {
        NotStarted,
        Running,
        Succeeded,
        Failed,
    }

    /// <summary>
    /// Gets or sets the sampler description.
    /// </summary>
    public string Description { get; protected set; }

    /// <summary>
    /// Checks whether activity needs to be created and tracked.
    /// </summary>
    /// <param name="samplingParameters">
    /// The <see cref="SamplingParameters"/> used by the <see cref="Sampler"/>
    /// to decide if the <see cref="Activity"/> to be created is going to be sampled or not.
    /// </param>
    /// <returns>Sampling decision on whether activity needs to be sampled or not.</returns>
    public abstract SamplingResult ShouldSample(in SamplingParameters samplingParameters);

    /// <summary>
    /// Attempts to shut down the sampler, giving it the opportunity to release any
    /// resources it holds. Blocks the current thread until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <see langword="true"/> when shutdown succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="timeoutMilliseconds"/> is smaller than -1.
    /// </exception>
    /// <remarks>
    /// <para>This function guarantees thread-safety. Only the first call will
    /// win, subsequent calls will be a no-op and return <see langword="false"/>.
    /// A call made while the first call is still running waits for it to finish, for up to
    /// its own <paramref name="timeoutMilliseconds"/>, before doing so.</para>
    /// <para>The <see cref="TracerProvider"/> calls this method when it is shut down or disposed
    /// and stops calling <see cref="ShouldSample"/> once it has begun to do so. A call to
    /// <see cref="ShouldSample"/> which is already in progress at that point may still complete,
    /// see <see cref="OnShutdown"/>. Samplers which delegate to other
    /// samplers are responsible for shutting down those delegates, see <see cref="OnShutdown"/>.</para>
    /// <para>The <see cref="TracerProvider"/> also disposes of the sampler if it implements
    /// <see cref="IDisposable"/>. <see cref="Sampler"/> itself does not implement
    /// <see cref="IDisposable"/> as most samplers do not hold any resources.</para>
    /// </remarks>
    public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);

        var result = this.ShutdownCore(timeoutMilliseconds, out var alreadyShutdown);

        return !alreadyShutdown && result;
    }

    /// <summary>
    /// Shuts down a sampler which is shared, such as a delegate of a built-in sampler
    /// or a process-wide instance chosen by configuration, and so may already have been shut down.
    /// </summary>
    /// <param name="sampler">The sampler to shut down.</param>
    /// <param name="timeoutMilliseconds">The timeout, see <see cref="Shutdown"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the sampler has been shut down successfully, whether by this call
    /// or by an earlier one (for example through another delegate, or by another provider which uses it);
    /// otherwise, <see langword="false"/>, including if shutting it down failed, or has not
    /// finished within the timeout.
    /// </returns>
    internal static bool ShutdownShared(Sampler sampler, int timeoutMilliseconds)
        => sampler.ShutdownCore(timeoutMilliseconds, out _);

    /// <summary>
    /// Called by <see cref="Shutdown"/>. This function should block the current
    /// thread until shutdown completed or timed out.
    /// </summary>
    /// <param name="timeoutMilliseconds">
    /// The number (non-negative) of milliseconds to wait, or
    /// <c>Timeout.Infinite</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// Returns <see langword="true"/> when shutdown succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>This function is called synchronously on the thread which made the
    /// first call to <see cref="Shutdown"/>. This function should not throw
    /// exceptions.</para>
    /// <para>Calls to <see cref="ShouldSample"/> which are already in progress may complete
    /// while this function runs, so implementations must keep them safe while resources
    /// are released. Implementations which do not return a static sampling decision should
    /// return <see cref="SamplingDecision.Drop"/> from <see cref="ShouldSample"/>
    /// once shutdown has begun.</para>
    /// <para>Implementations which delegate to other samplers should call
    /// <see cref="Shutdown"/> on each of them.</para>
    /// </remarks>
    protected virtual bool OnShutdown(int timeoutMilliseconds) => true;

    private bool ShutdownCore(int timeoutMilliseconds, out bool alreadyShutdown)
    {
        // A call made while another is running shutdown waits for it to finish, for up to its own timeout
        if (!this.shutdownLock.TryEnter(timeoutMilliseconds))
        {
            alreadyShutdown = true;
            return false;
        }

        try
        {
            if (this.shutdownState != ShutdownState.NotStarted)
            {
                // Report the outcome of the shutdown. If it is still running, then
                // this is a re-entrant call from OnShutdown, which has not succeeded yet.
                alreadyShutdown = true;
                return this.shutdownState == ShutdownState.Succeeded;
            }

            alreadyShutdown = false;
            this.shutdownState = ShutdownState.Running;

            var result = false;

            try
            {
                result = this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.TracerProviderException(nameof(this.Shutdown), ex);
            }

            this.shutdownState = result ? ShutdownState.Succeeded : ShutdownState.Failed;

            return result;
        }
        finally
        {
            this.shutdownLock.Exit();
        }
    }
}
