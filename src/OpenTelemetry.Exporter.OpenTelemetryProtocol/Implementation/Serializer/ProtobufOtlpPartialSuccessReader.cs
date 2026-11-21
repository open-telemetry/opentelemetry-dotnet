// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Reads the <c>partial_success</c> field of an OTLP export response.
/// </summary>
/// <remarks>
/// <c>ExportTraceServiceResponse</c>, <c>ExportMetricsServiceResponse</c> and
/// <c>ExportLogsServiceResponse</c> share the same layout: field 1 holds a
/// <c>Export*PartialSuccess</c> message whose field 1 is the number of rejected
/// items (<c>int64</c>) and whose field 2 is an error message (<c>string</c>).
/// All other fields are skipped.
/// </remarks>
internal static class ProtobufOtlpPartialSuccessReader
{
    internal const int ResponsePartialSuccessFieldNumber = 1;
    internal const int PartialSuccessRejectedFieldNumber = 1;
    internal const int PartialSuccessErrorMessageFieldNumber = 2;

    // A varint is at most 10 bytes long.
    private const int MaxVarIntLength = 10;

    /// <summary>
    /// Tries to read the partial success of a serialized OTLP export response.
    /// </summary>
    /// <param name="buffer">The buffer holding the response message.</param>
    /// <param name="offset">The position of the message in <paramref name="buffer"/>.</param>
    /// <param name="count">The length of the message.</param>
    /// <param name="rejectedCount">The number of rejected items, or zero if not set.</param>
    /// <param name="errorMessage">The error message, or <see langword="null"/> if not set or empty.</param>
    /// <returns><see langword="true"/> if the message was well-formed; otherwise <see langword="false"/>.</returns>
    public static bool TryRead(byte[] buffer, int offset, int count, out long rejectedCount, out string? errorMessage)
    {
        rejectedCount = 0;
        errorMessage = null;

        if (buffer is null || offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            return false;
        }

        var position = offset;
        var end = offset + count;

        while (position < end)
        {
            if (!TryReadTag(buffer, ref position, end, out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == ResponsePartialSuccessFieldNumber && wireType == ProtobufWireType.LEN)
            {
                if (!TryReadLength(buffer, ref position, end, out var length))
                {
                    return false;
                }

                // Repeated occurrences of an embedded message are merged, so the last value of each field wins.
                if (!TryReadPartialSuccess(buffer, position, position + length, ref rejectedCount, ref errorMessage))
                {
                    return false;
                }

                position += length;
            }
            else if (!TrySkipField(buffer, ref position, end, wireType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadPartialSuccess(byte[] buffer, int position, int end, ref long rejectedCount, ref string? errorMessage)
    {
        while (position < end)
        {
            if (!TryReadTag(buffer, ref position, end, out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == PartialSuccessRejectedFieldNumber && wireType == ProtobufWireType.VARINT)
            {
                if (!TryReadVarInt64(buffer, ref position, end, out var value))
                {
                    return false;
                }

                rejectedCount = unchecked((long)value);
            }
            else if (fieldNumber == PartialSuccessErrorMessageFieldNumber && wireType == ProtobufWireType.LEN)
            {
                if (!TryReadLength(buffer, ref position, end, out var length))
                {
                    return false;
                }

                errorMessage = length == 0 ? null : Encoding.UTF8.GetString(buffer, position, length);
                position += length;
            }
            else if (!TrySkipField(buffer, ref position, end, wireType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadTag(byte[] buffer, ref int position, int end, out int fieldNumber, out ProtobufWireType wireType)
    {
        fieldNumber = 0;
        wireType = default;

        if (!TryReadVarInt64(buffer, ref position, end, out var tag) || tag > uint.MaxValue)
        {
            return false;
        }

        fieldNumber = (int)(tag >> 3);
        wireType = (ProtobufWireType)(tag & 0x7);

        // Field number zero is not valid.
        return fieldNumber > 0;
    }

    private static bool TryReadLength(byte[] buffer, ref int position, int end, out int length)
    {
        length = 0;

        if (!TryReadVarInt64(buffer, ref position, end, out var value) || value > (ulong)(end - position))
        {
            return false;
        }

        length = (int)value;
        return true;
    }

    private static bool TrySkipField(byte[] buffer, ref int position, int end, ProtobufWireType wireType)
    {
        switch (wireType)
        {
            case ProtobufWireType.VARINT:
                return TryReadVarInt64(buffer, ref position, end, out _);

            case ProtobufWireType.I64:
                return TrySkipBytes(ref position, end, 8);

            case ProtobufWireType.LEN:
                if (!TryReadLength(buffer, ref position, end, out var length))
                {
                    return false;
                }

                position += length;
                return true;

            case ProtobufWireType.I32:
                return TrySkipBytes(ref position, end, 4);

            default:
                // Groups are deprecated and not used by OTLP, and other values are not valid.
                return false;
        }
    }

    private static bool TrySkipBytes(ref int position, int end, int count)
    {
        if (end - position < count)
        {
            return false;
        }

        position += count;
        return true;
    }

    private static bool TryReadVarInt64(byte[] buffer, ref int position, int end, out ulong value)
    {
        value = 0;

        for (var i = 0; i < MaxVarIntLength; i++)
        {
            if (position >= end)
            {
                return false;
            }

            var b = buffer[position++];

            // The tenth byte only carries the 64th bit, anything more overflows a ulong.
            if (i == MaxVarIntLength - 1 && b > 0x01)
            {
                return false;
            }

            value |= (ulong)(b & 0x7F) << (7 * i);

            if ((b & 0x80) == 0)
            {
                return true;
            }
        }

        // More than 10 bytes is not a valid varint.
        return false;
    }
}
