// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods on Activity.
/// </summary>
internal static class ActivityHelperExtensions
{
    /// <summary>
    /// Gets the value of a specific tag on an <see cref="Activity"/>.
    /// </summary>
    /// <param name="activity">Activity instance.</param>
    /// <param name="tagName">Case-sensitive tag name to retrieve.</param>
    /// <returns>Tag value or null if a match was not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? GetTagValue(this Activity activity, string? tagName)
    {
        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            if (tag.Key == tagName)
            {
                return tag.Value;
            }
        }

        return null;
    }
}
