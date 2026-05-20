// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET10_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfills the GetOrAdd APIs added to <see cref="ConditionalWeakTable{TKey, TValue}"/> in .NET 10.
/// See https://github.com/dotnet/runtime/pull/111204.
/// </summary>
internal static class ConditionalWeakTableExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue GetOrAdd<TKey, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TValue>(
        this ConditionalWeakTable<TKey, TValue> table,
        TKey key,
        ConditionalWeakTable<TKey, TValue>.CreateValueCallback valueFactory)
        where TKey : class
        where TValue : class
        => table.GetValue(key, valueFactory);
}

#endif
