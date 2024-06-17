// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal class Writer
{
    private const uint Uint128 = 128;
    private const ulong Ulong128 = 128;
    private const int Fixed64Size = 8;

    internal static Encoding Utf8Encoding => Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringTag(ref byte[] buffer, int cursor, int fieldNumber, string value)
    {
        int stringSize = Utf8Encoding.GetByteCount(value);

        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.LEN);

        cursor = WriteLength(ref buffer, cursor, stringSize);

        if (cursor + stringSize > buffer.Length)
        {
            byte[] values = Utf8Encoding.GetBytes(value);

            foreach (var v in values)
            {
                cursor = WriteSingleByte(ref buffer, cursor, v);
            }

            return cursor;
        }
        else
        {
            _ = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, cursor);

            cursor += stringSize;

            return cursor;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTagAndLengthPrefix(ref byte[] buffer, int cursor, int contentLength, int fieldNumber, WireType type)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, type);
        cursor = WriteLength(ref buffer, cursor, contentLength);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteEnumWithTag(ref byte[] buffer, int cursor, int fieldNumber, int value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.VARINT);

        // Assuming 1 byte which matches the intended use.
        cursor = WriteSingleByte(ref buffer, cursor, (byte)value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTag(ref byte[] buffer, int cursor, int fieldNumber, WireType type)
    {
        cursor = WriteVarint32(ref buffer, cursor, GetTagValue(fieldNumber, type));
        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLength(ref byte[] buffer, int cursor, int length)
    {
        cursor = WriteVarint32(ref buffer, cursor, (uint)length);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64WithTag(ref byte[] buffer, int cursor, int fieldNumber, ulong value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.I64);
        cursor = WriteFixed64LittleEndianFormat(ref buffer, cursor, value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32WithTag(ref byte[] buffer, int cursor, int fieldNumber, uint value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.I32);
        cursor = WriteFixed32LittleEndianFormat(ref buffer, cursor, value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteBoolWithTag(ref byte[] buffer, int cursor, int fieldNumber, bool value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.VARINT);

        cursor = WriteSingleByte(ref buffer, cursor, value ? (byte)1 : (byte)0);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarint32(ref byte[] buffer, int cursor, uint value)
    {
        while (cursor < buffer.Length && value >= Uint128)
        {
            buffer[cursor++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        if (cursor < buffer.Length)
        {
            buffer[cursor++] = (byte)value;
            return cursor;
        }

        // Handle case of insufficient buffer space.
        while (value >= Uint128)
        {
            cursor = WriteSingleByte(ref buffer, cursor, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        cursor = WriteSingleByte(ref buffer, cursor, (byte)value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarint64(ref byte[] buffer, int cursor, ulong value)
    {
        while (cursor < buffer.Length && value >= Ulong128)
        {
            buffer[cursor++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        if (cursor < buffer.Length)
        {
            buffer[cursor++] = (byte)value;
            return cursor;
        }

        // Handle case of insufficient buffer space.
        while (value >= Ulong128)
        {
            cursor = WriteSingleByte(ref buffer, cursor, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        cursor = WriteSingleByte(ref buffer, cursor, (byte)value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteInt64WithTag(ref byte[] buffer, int cursor, int fieldNumber, ulong value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.VARINT);
        cursor = WriteVarint64(ref buffer, cursor, value);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteDoubleWithTag(ref byte[] buffer, int cursor, int fieldNumber, double value)
    {
        cursor = WriteTag(ref buffer, cursor, fieldNumber, WireType.I64);
        cursor = WriteFixed64LittleEndianFormat(ref buffer, cursor, (ulong)BitConverter.DoubleToInt64Bits(value));

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64LittleEndianFormat(ref byte[] buffer, int cursor, ulong value)
    {
        if (cursor + Fixed64Size <= buffer.Length)
        {
            Span<byte> span = new Span<byte>(buffer, cursor, Fixed64Size);

            BinaryPrimitives.WriteUInt64LittleEndian(span, value);

            cursor += Fixed64Size;
        }
        else
        {
            // Write byte by byte.
            cursor = WriteSingleByte(ref buffer, cursor, (byte)value);
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 8));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 16));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 24));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 32));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 40));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 48));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 56));
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32LittleEndianFormat(ref byte[] buffer, int cursor, uint value)
    {
        if (cursor + 4 <= buffer.Length)
        {
            Span<byte> span = new Span<byte>(buffer, cursor, 4);

            BinaryPrimitives.WriteUInt32LittleEndian(span, value);

            cursor += 4;
        }
        else
        {
            // Write byte by byte.
            cursor = WriteSingleByte(ref buffer, cursor, (byte)value);
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 8));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 16));
            cursor = WriteSingleByte(ref buffer, cursor, (byte)(value >> 24));
        }

        return cursor;
    }

    internal static uint GetTagValue(int fieldNumber, WireType wireType)
    {
        return ((uint)(fieldNumber << 3)) | (uint)wireType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLengthCustom(ref byte[] buffer, int cursor, int length)
    {
        cursor = WriteVarintCustom(ref buffer, cursor, (uint)length);

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarintCustom(ref byte[] buffer, int cursor, uint value)
    {
        int index = 0;

        // Loop until all 7 bits from the integer value have been encoded
        while (value > 0)
        {
            byte chunk = (byte)(value & 0x7F); // Extract the least significant 7 bits
            value >>= 7; // Right shift the value by 7 bits to process the next chunk

            // If there are more bits to encode, set the most significant bit to 1
            if (index < 3)
            {
                chunk |= 0x80;
            }

            buffer[cursor++] = chunk; // Store the encoded chunk
            index++;
        }

        // If fewer than 3 bytes were used, pad with zeros
        while (index < 2)
        {
            buffer[cursor++] = 0x80;
            index++;
        }

        while (index < 3)
        {
            buffer[cursor++] = 0x00;
            index++;
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteSingleByte(ref byte[] buffer, int cursor, byte value)
    {
        if (buffer.Length == cursor)
        {
            RefreshBuffer(ref buffer);
        }

        buffer[cursor++] = value;

        return cursor;
    }

    internal static void RefreshBuffer(ref byte[] buffer)
    {
        var newBuffer = new byte[buffer.Length * 2];

        Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);

        buffer = newBuffer;
    }
}

