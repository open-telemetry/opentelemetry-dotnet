// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text;

#nullable enable

namespace OpenTelemetry.Context.Propagation;

// Encodes baggage values according to the https://www.w3.org/TR/baggage/#value spec.
// This is a modified code of WebUtility.Encode, which handles space char differently -
// it is percent-encoded, instead of being converted to '+'.
// Additionally, allowed characters from baggage-octet range from the spec are not percent-encoded.
// Encoding baggage value with this encoder yields minimal, spec-compliant representation.

internal static class W3CBaggageValueEncoder
{
    // originated from https://github.com/dotnet/runtime/blob/9429e432f39786e1bcd1c080833e5d7691946591/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs#L368

    // Modified to percent-encode space char instead of converting it to '+'.
    public static string? Encode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        int safeCount = 0;
        for (int i = 0; i < value!.Length; i++)
        {
            char ch = value[i];
            if (!RequiresPercentEncoding(ch))
            {
                safeCount++;
            }
        }

        if (safeCount == value.Length)
        {
            // Nothing to expand
            return value;
        }

        int byteCount = Encoding.UTF8.GetByteCount(value);
        int unsafeByteCount = byteCount - safeCount;
        int byteIndex = unsafeByteCount * 2;

        // comment originated from https://github.com/dotnet/runtime/blob/9429e432f39786e1bcd1c080833e5d7691946591/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs#L405

        // Instead of allocating one array of length `byteCount` to store
        // the UTF-8 encoded bytes, and then a second array of length
        // `3 * byteCount - 2 * unexpandedCount`
        // to store the URL-encoded UTF-8 bytes, we allocate a single array of
        // the latter and encode the data in place, saving the first allocation.
        // We store the UTF-8 bytes to the end of this array, and then URL encode to the
        // beginning of the array.
        byte[] newBytes = new byte[byteCount + byteIndex];
        Encoding.UTF8.GetBytes(value, 0, value.Length, newBytes, byteIndex);

        GetEncodedBytes(newBytes, byteIndex, byteCount, newBytes);
        return Encoding.UTF8.GetString(newBytes);
    }

    private static bool RequiresPercentEncoding(char ch)
    {
        // The percent char MUST be percent-encoded.
        return ch == '%' || !IsInBaggageOctetRange(ch);
    }

    private static bool IsInBaggageOctetRange(char ch)
    {
        // from the spec:
        // Any characters outside of the baggage-octet range of characters MUST be percent-encoded.
        // baggage-octet          =  %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
        return ch == '!' || // 0x21
               (ch >= '#' && ch <= '+') || // 0x23-0x2B
               (ch >= '-' && ch <= ':') || // 0x2D-0x3A
               (ch >= '<' && ch <= '[') || // 0x3C-0x5B
               (ch >= ']' && ch <= '~');   // 0x5D-0x7E
    }

    // originated from https://github.com/dotnet/runtime/blob/9429e432f39786e1bcd1c080833e5d7691946591/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs#L328
    // Modified to percent-encode the space char.
    private static void GetEncodedBytes(byte[] originalBytes, int offset, int count, byte[] expandedBytes)
    {
        int pos = 0;
        int end = offset + count;
        for (int i = offset; i < end; i++)
        {
            byte b = originalBytes[i];
            char ch = (char)b;
            if (RequiresPercentEncoding(ch))
            {
                expandedBytes[pos++] = (byte)'%';
                expandedBytes[pos++] = (byte)ToCharUpper(b >> 4);
                expandedBytes[pos++] = (byte)ToCharUpper(b);
            }
            else
            {
                expandedBytes[pos++] = b;
            }
        }
    }

    // originated from https://github.com/dotnet/runtime/blob/9429e432f39786e1bcd1c080833e5d7691946591/src/libraries/Common/src/System/HexConverter.cs#L203
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToCharUpper(int value)
    {
        value &= 0xF;
        value += '0';

        if (value > '9')
        {
            value += 'A' - ('9' + 1);
        }

        return (char)value;
    }
}
