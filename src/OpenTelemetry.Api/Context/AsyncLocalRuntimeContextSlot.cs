// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Context;

/// <summary>
/// The async local implementation of context slot.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public class AsyncLocalRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    private readonly AsyncLocal<T> slot;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLocalRuntimeContextSlot{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the context slot.</param>
    public AsyncLocalRuntimeContextSlot(string name)
        : base(name)
    {
        this.slot = new AsyncLocal<T>();
    }

    /// <inheritdoc/>
    public object? Value
    {
        get => this.slot.Value;
        set
        {
            if (typeof(T).IsValueType && value is null)
            {
                this.slot.Value = default!;
            }
            else
            {
                this.slot.Value = (T)value!;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T? Get()
    {
        return this.slot.Value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        this.slot.Value = value;
    }
}
