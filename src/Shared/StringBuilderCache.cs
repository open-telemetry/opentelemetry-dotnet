// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// SOURCE: https://github.com/dotnet/runtime/blob/66b796a335cab4e638790d1af7ecd58a83d0d5ae/src/libraries/Common/src/System/Text/StringBuilderCache.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace OpenTelemetry.Internal;

internal static class StringBuilderCache
{
    // The value 360 was chosen in discussion with performance experts as a compromise
    // between using as little memory per thread as possible and still covering a large
    // part of short-lived StringBuilder creations on the startup path of VS designers.
    internal const int MaxBuilderSize = 360;
    private const int DefaultCapacity = 64;

    [ThreadStatic]
    private static StringBuilder? cachedInstance;

    /// <summary>Get a StringBuilder for the specified capacity.</summary>
    /// <remarks>If a StringBuilder of an appropriate size is cached, it will be returned and the cache emptied.</remarks>
    /// <param name="capacity">The minimum capacity required.</param>
    /// <returns>A <see cref="StringBuilder"/> with at least the specified capacity.</returns>
    public static StringBuilder Acquire(int capacity = DefaultCapacity)
    {
        if (capacity <= MaxBuilderSize)
        {
            var sb = cachedInstance;
            if (sb != null)
            {
                // Avoid StringBuilder block fragmentation by getting a new StringBuilder
                // when the requested size is larger than the current capacity
                if (capacity <= sb.Capacity)
                {
                    cachedInstance = null;
                    sb.Clear();
                    return sb;
                }
            }
        }

        return new StringBuilder(capacity);
    }

    /// <summary>Place the specified builder in the cache if it is not too big.</summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to return to the cache.</param>
    public static void Release(StringBuilder sb)
    {
        if (sb.Capacity <= MaxBuilderSize)
        {
            cachedInstance = sb;
        }
    }

    /// <summary>ToString() the StringBuilder, Release it to the cache, and return the resulting string.</summary>
    /// <param name="sb">The <see cref="StringBuilder"/> whose contents to return.</param>
    /// <returns>The string contents of <paramref name="sb"/>.</returns>
    public static string GetStringAndRelease(StringBuilder sb)
    {
        var result = sb.ToString();
        Release(sb);
        return result;
    }
}
