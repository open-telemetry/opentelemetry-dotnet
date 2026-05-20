// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK || NETSTANDARD2_0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text;

/// <summary>
/// Polyfills span-based <see cref="Encoding"/> APIs missing on legacy targets. Mirrors
/// https://github.com/dotnet/runtime/blob/74b0096b51f360e7650ed0b347fb18cabe75498a/src/libraries/Common/src/Polyfills/EncodingPolyfills.cs.
/// </summary>
internal static class EncodingExtensions
{
#pragma warning disable SA1101 // Prefix local calls with this - extension receiver is the parameter, not 'this'.
#pragma warning disable SA1519 // Braces should not be omitted - chained 'fixed' statements are idiomatic.
    extension(Encoding encoding)
    {
        public unsafe int GetByteCount(ReadOnlySpan<char> chars)
        {
            fixed (char* charsPtr = &GetNonNullPinnableReference(chars))
            {
                return encoding.GetByteCount(charsPtr, chars.Length);
            }
        }

        public unsafe int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            fixed (char* charsPtr = &GetNonNullPinnableReference(chars))
            fixed (byte* bytesPtr = &GetNonNullPinnableReference(bytes))
            {
                return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
            }
        }
    }
#pragma warning restore SA1519
#pragma warning restore SA1101

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref readonly T GetNonNullPinnableReference<T>(ReadOnlySpan<T> buffer)
    {
        // Based on the internal implementation from MemoryMarshal.
        return ref buffer.Length != 0 ? ref MemoryMarshal.GetReference(buffer) : ref Unsafe.AsRef<T>((void*)1);
    }
}

#endif
