// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

internal static class InterlockedHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref double location, double value)
    {
        double currentValue = Volatile.Read(ref location);
        while (true)
        {
            var returnedValue = Interlocked.CompareExchange(ref location, currentValue + value, currentValue);
            if (returnedValue == currentValue)
            {
                break;
            }

            currentValue = returnedValue;
        }
    }
}
