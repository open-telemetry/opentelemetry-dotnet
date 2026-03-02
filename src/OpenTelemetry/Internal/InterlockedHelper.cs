// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

internal static class InterlockedHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref double location, double value)
    {
        // Note: Not calling InterlockedHelper.Read here on purpose because it
        // is too expensive for fast/happy-path. If the first attempt fails
        // we'll end up in an Interlocked.CompareExchange loop anyway.
        double currentValue = Volatile.Read(ref location);

        var returnedValue = Interlocked.CompareExchange(ref location, currentValue + value, currentValue);
        if (returnedValue != currentValue)
        {
            AddRare(ref location, value, returnedValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Read(ref double location)
        => Interlocked.CompareExchange(ref location, double.NaN, double.NaN);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddRare(ref double location, double value, double currentValue)
    {
        var sw = default(SpinWait);
        while (true)
        {
            sw.SpinOnce(-1);

            var returnedValue = Interlocked.CompareExchange(ref location, currentValue + value, currentValue);
            if (returnedValue == currentValue)
            {
                break;
            }

            currentValue = returnedValue;
        }
    }
}
