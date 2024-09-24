// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
#nullable enable

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;

namespace OpenTelemetry.Context;

/// <summary>
/// The .NET Remoting implementation of context slot.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
public class RemotingRuntimeContextSlot<T> : RuntimeContextSlot<T>, IRuntimeContextSlotValueAccessor
{
    // A special workaround to suppress context propagation cross AppDomains.
    //
    // By default the value added to System.Runtime.Remoting.Messaging.CallContext
    // will be marshalled/unmarshalled across AppDomain boundary. This will cause
    // serious issue if the destination AppDomain doesn't have the corresponding type
    // to unmarshal data.
    // The worst case is AppDomain crash with ReflectionLoadTypeException.
    //
    // The workaround is to use a well known type that exists in all AppDomains, and
    // put the actual payload as a non-public field so the field is ignored during
    // marshalling.
    private static readonly FieldInfo WrapperField = typeof(BitArray).GetField("_syncRoot", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// Initializes a new instance of the <see cref="RemotingRuntimeContextSlot{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the context slot.</param>
    public RemotingRuntimeContextSlot(string name)
        : base(name)
    {
    }

    /// <inheritdoc/>
    public object? Value
    {
        get => this.Get();
        set
        {
            if (typeof(T).IsValueType && value is null)
            {
                this.Set(default!);
            }
            else
            {
                this.Set((T)value!);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override T? Get()
    {
        if (CallContext.LogicalGetData(this.Name) is not BitArray wrapper)
        {
            return default;
        }

        var value = WrapperField.GetValue(wrapper);

        if (typeof(T).IsValueType && value is null)
        {
            return default;
        }

        return (T)value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Set(T value)
    {
        var wrapper = new BitArray(0);
        WrapperField.SetValue(wrapper, value);
        CallContext.LogicalSetData(this.Name, wrapper);
    }
}
#endif
