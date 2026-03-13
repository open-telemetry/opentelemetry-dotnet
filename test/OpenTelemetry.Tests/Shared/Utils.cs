// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Tests;

internal static class Utils
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetCurrentMethodName()
    {
        var method = new StackFrame(1).GetMethod();
        return $"{method?.DeclaringType?.FullName}.{method?.Name}";
    }
}
