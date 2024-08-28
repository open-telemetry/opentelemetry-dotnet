// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context;

/// <summary>
/// Generic runtime context management API.
/// </summary>
public static class RuntimeContext
{
    private static readonly ConcurrentDictionary<string, object> Slots = new();

    private static Type contextSlotType = typeof(AsyncLocalRuntimeContextSlot<>);

    /// <summary>
    /// Gets or sets the actual context carrier implementation.
    /// </summary>
    public static Type ContextSlotType
    {
        get => contextSlotType;
        set
        {
            Guard.ThrowIfNull(value, nameof(value));

            if (value == typeof(AsyncLocalRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
            else if (value == typeof(ThreadLocalRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
#if NETFRAMEWORK
            else if (value == typeof(RemotingRuntimeContextSlot<>))
            {
                contextSlotType = value;
            }
#endif
            else
            {
                throw new NotSupportedException($"{value} is not a supported type.");
            }
        }
    }

    /// <summary>
    /// Register a named context slot.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    /// <returns>The slot registered.</returns>
    public static RuntimeContextSlot<T>? RegisterSlot<T>(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);
        RuntimeContextSlot<T>? slot = null;

        lock (Slots)
        {
            if (Slots.ContainsKey(slotName))
            {
                throw new InvalidOperationException($"Context slot already registered: '{slotName}'");
            }

            if (ContextSlotType == typeof(AsyncLocalRuntimeContextSlot<>))
            {
                slot = new AsyncLocalRuntimeContextSlot<T>(slotName);
            }
            else if (ContextSlotType == typeof(ThreadLocalRuntimeContextSlot<>))
            {
                slot = new ThreadLocalRuntimeContextSlot<T>(slotName);
            }

#if NETFRAMEWORK
            else if (ContextSlotType == typeof(RemotingRuntimeContextSlot<>))
            {
                slot = new RemotingRuntimeContextSlot<T>(slotName);
            }
#endif

            Slots[slotName] = slot!;
            return slot;
        }
    }

    /// <summary>
    /// Get a registered slot from a given name.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    /// <returns>The slot previously registered.</returns>
    public static RuntimeContextSlot<T> GetSlot<T>(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);
        var slot = GuardNotFound(slotName);
        var contextSlot = Guard.ThrowIfNotOfType<RuntimeContextSlot<T>>(slot);
        return contextSlot;
    }

    /*
    public static void Apply(IDictionary<string, object> snapshot)
    {
        foreach (var entry in snapshot)
        {
            // TODO: revisit this part if we want Snapshot() to be used on critical paths
            dynamic value = entry.Value;
            SetValue(entry.Key, value);
        }
    }

    public static IDictionary<string, object> Snapshot()
    {
        var retval = new Dictionary<string, object>();
        foreach (var entry in Slots)
        {
            // TODO: revisit this part if we want Snapshot() to be used on critical paths
            dynamic slot = entry.Value;
            retval[entry.Key] = slot.Get();
        }
        return retval;
    }
    */

    /// <summary>
    /// Sets the value to a registered slot.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <param name="value">The value to be set.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue<T>(string slotName, T value)
    {
        GetSlot<T>(slotName).Set(value);
    }

    /// <summary>
    /// Gets the value from a registered slot.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The value retrieved from the context slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetValue<T>(string slotName)
    {
        return GetSlot<T>(slotName).Get();
    }

    /// <summary>
    /// Sets the value to a registered slot.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <param name="value">The value to be set.</param>
    public static void SetValue(string slotName, object value)
    {
        Guard.ThrowIfNullOrEmpty(slotName);
        var slot = GuardNotFound(slotName);
        var runtimeContextSlotValueAccessor = Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot);
        runtimeContextSlotValueAccessor.Value = value;
    }

    /// <summary>
    /// Gets the value from a registered slot.
    /// </summary>
    /// <param name="slotName">The name of the context slot.</param>
    /// <returns>The value retrieved from the context slot.</returns>
    public static object? GetValue(string slotName)
    {
        Guard.ThrowIfNullOrEmpty(slotName);
        var slot = GuardNotFound(slotName);
        var runtimeContextSlotValueAccessor = Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot);
        return runtimeContextSlotValueAccessor.Value;
    }

    // For testing purpose
    internal static void Clear()
    {
        Slots.Clear();
    }

    private static object GuardNotFound(string slotName)
    {
        if (!Slots.TryGetValue(slotName, out var slot))
        {
            throw new ArgumentException($"Context slot not found: '{slotName}'");
        }

        return slot;
    }
}
