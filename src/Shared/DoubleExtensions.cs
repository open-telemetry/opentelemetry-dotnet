// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET

using System.Runtime.CompilerServices;

namespace System;

internal static class DoubleExtensions
{
    extension(double)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFinite(double value) =>
            !double.IsInfinity(value) && !double.IsNaN(value);
    }
}

#endif
