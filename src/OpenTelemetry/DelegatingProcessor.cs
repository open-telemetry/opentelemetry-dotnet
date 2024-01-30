// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// A <see cref="BaseProcessor{T}"/> implementation which will forward calls to
/// an inner <see cref="BaseProcessor{T}"/>.
/// </summary>
/// <typeparam name="T">The type of object to be processed.</typeparam>
public abstract class DelegatingProcessor<T> : BaseProcessor<T>
{
    internal readonly BaseProcessor<T> InnerProcessor;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatingProcessor{T}"/> class.
    /// </summary>
    /// <param name="innerProcessor"><see cref="BaseProcessor{T}"/>.</param>
    protected DelegatingProcessor(BaseProcessor<T> innerProcessor)
    {
        Guard.ThrowIfNull(innerProcessor);

        this.InnerProcessor = innerProcessor;
    }

    /// <inheritdoc/>
    public override void OnStart(T data)
    {
        this.InnerProcessor.OnStart(data);
    }

    /// <inheritdoc/>
    public override void OnEnd(T data)
    {
        this.InnerProcessor.OnEnd(data);
    }

    /// <inheritdoc/>
    internal override void SetParentProvider(BaseProvider parentProvider)
    {
        this.InnerProcessor.SetParentProvider(parentProvider);
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        return this.InnerProcessor.ForceFlush(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.InnerProcessor.Shutdown(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.InnerProcessor.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
