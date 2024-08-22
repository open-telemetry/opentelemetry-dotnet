// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Context;

/// <summary>
/// The thread local (TLS) implementation of context slot.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public class ThreadLocalRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    private readonly ThreadLocal<T> slot;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadLocalRuntimeContextSlot{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the context slot.</param>
    public ThreadLocalRuntimeContextSlot(string name)
        : base(name)
    {
        this.slot = new ThreadLocal<T>();
    }

    /// <inheritdoc/>
    public object? Value
    {
        get => this.slot.Value;
        set => this.slot.Value = (T)value!;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T Get()
    {
        return this.slot.Value!;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        this.slot.Value = value;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.slot.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
