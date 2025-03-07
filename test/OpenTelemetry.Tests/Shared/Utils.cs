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

        Debug.Assert(method != null, "Failed to get Method from the executing stack.");
        Debug.Assert(method!.DeclaringType != null, "DeclaringType is not expected to be null.");

        return $"{method.DeclaringType!.FullName}.{method.Name}";
    }
}
