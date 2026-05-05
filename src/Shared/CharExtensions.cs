// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET

using System.Runtime.CompilerServices;

namespace System;

internal static class CharExtensions
{
    extension(char)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAsciiDigit(char value) =>
            value is >= '0' and <= '9';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAsciiLetterOrDigit(char value) =>
            value is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAsciiLetterLower(char value) =>
            value is >= 'a' and <= 'z';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAsciiLetterUpper(char value) =>
            value >= 'A' && value <= 'Z';
    }
}

#endif
