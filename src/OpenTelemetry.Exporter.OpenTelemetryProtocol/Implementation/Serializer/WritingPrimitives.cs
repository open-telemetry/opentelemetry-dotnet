// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

[SecuritySafeCritical]
internal static class WritingPrimitives
{
    private const uint UInt128 = 0x80;
    private const ulong ULong128 = 0x80;
    private const int Fixed32Size = 4;
    private const int Fixed64Size = 8;
    private const int ReservedLengthSize = 4;

#if NET
    private static Encoding Utf8Encoding => Encoding.UTF8;
#else
    private static readonly Encoding Utf8Encoding = Encoding.UTF8;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetTagValue(int fieldNumber, WireType wireType) => ((uint)(fieldNumber << 3)) | (uint)wireType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTag(ref byte[] buffer, int writePosition, int fieldNumber, WireType type) => WriteVarInt32(ref buffer, writePosition, GetTagValue(fieldNumber, type));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLength(ref byte[] buffer, int writePosition, int length) => WriteVarInt32(ref buffer, writePosition, (uint)length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTagAndLengthPrefix(ref byte[] buffer, int writePosition, int contentLength, int fieldNumber, WireType type)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, type);
        writePosition = WriteLength(ref buffer, writePosition, contentLength);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteReservedLength(ref byte[] buffer, int writePosition, int length)
    {
        int byteLength = 0;
        int? firstByte = null;
        int? secondByte = null;
        int? thirdByte = null;
        int? fourthByte = null;

        do
        {
            switch (byteLength)
            {
                case 0:
                    firstByte = length & 0x7F;
                    break;
                case 1:
                    secondByte = length & 0x7F;
                    break;
                case 2:
                    thirdByte = length & 0x7F;
                    break;
                case 3:
                    fourthByte = length & 0x7F;
                    break;
            }

            length >>= 7;
            byteLength++;
        }
        while (length > 0);

        EnsureBufferCapacity(ref buffer, writePosition + ReservedLengthSize);

        if (fourthByte.HasValue)
        {
            buffer[writePosition++] = (byte)(firstByte!.Value | 0x80);
            buffer[writePosition++] = (byte)(secondByte!.Value | 0x80);
            buffer[writePosition++] = (byte)(thirdByte!.Value | 0x80);
            buffer[writePosition++] = (byte)fourthByte!.Value;
        }
        else if (thirdByte.HasValue)
        {
            buffer[writePosition++] = (byte)(firstByte!.Value | 0x80);
            buffer[writePosition++] = (byte)(secondByte!.Value | 0x80);
            buffer[writePosition++] = (byte)(thirdByte!.Value | 0x80);
            buffer[writePosition++] = 0;
        }
        else if (secondByte.HasValue)
        {
            buffer[writePosition++] = (byte)(firstByte!.Value | 0x80);
            buffer[writePosition++] = (byte)(secondByte!.Value | 0x80);
            buffer[writePosition++] = 0x80;
            buffer[writePosition++] = 0;
        }
        else
        {
            buffer[writePosition++] = (byte)(firstByte!.Value | 0x80);
            buffer[writePosition++] = 0x80;
            buffer[writePosition++] = 0x80;
            buffer[writePosition++] = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteBoolWithTag(ref byte[] buffer, int writePosition, int fieldNumber, bool value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.VARINT);
        writePosition = WriteSingleByte(ref buffer, writePosition, value ? (byte)1 : (byte)0);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteEnumWithTag(ref byte[] buffer, int writePosition, int fieldNumber, int value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.VARINT);

        // Assuming 1 byte which matches the intended use.
        writePosition = WriteSingleByte(ref buffer, writePosition, (byte)value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32LittleEndianFormat(ref byte[] buffer, int writePosition, uint value)
    {
        EnsureBufferCapacity(ref buffer, writePosition + Fixed32Size);
        Span<byte> span = new(buffer, writePosition, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        writePosition += Fixed32Size;

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64LittleEndianFormat(ref byte[] buffer, int writePosition, ulong value)
    {
        EnsureBufferCapacity(ref buffer, writePosition + Fixed64Size);
        Span<byte> span = new(buffer, writePosition, Fixed64Size);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        writePosition += Fixed64Size;

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32WithTag(ref byte[] buffer, int writePosition, int fieldNumber, uint value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.I32);
        writePosition = WriteFixed32LittleEndianFormat(ref buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64WithTag(ref byte[] buffer, int writePosition, int fieldNumber, ulong value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.I64);
        writePosition = WriteFixed64LittleEndianFormat(ref buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarInt32(ref byte[] buffer, int writePosition, uint value)
    {
        while (writePosition < buffer.Length && value >= UInt128)
        {
            buffer[writePosition++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        if (writePosition < buffer.Length)
        {
            buffer[writePosition++] = (byte)value;
            return writePosition;
        }

        // Handle case of insufficient buffer space.
        while (value >= UInt128)
        {
            writePosition = WriteSingleByte(ref buffer, writePosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        writePosition = WriteSingleByte(ref buffer, writePosition, (byte)value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarInt64(ref byte[] buffer, int writePosition, ulong value)
    {
        while (writePosition < buffer.Length && value >= ULong128)
        {
            buffer[writePosition++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        if (writePosition < buffer.Length)
        {
            buffer[writePosition++] = (byte)value;
            return writePosition;
        }

        // Handle case of insufficient buffer space.
        while (value >= ULong128)
        {
            writePosition = WriteSingleByte(ref buffer, writePosition, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        writePosition = WriteSingleByte(ref buffer, writePosition, (byte)value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteInt64WithTag(ref byte[] buffer, int writePosition, int fieldNumber, ulong value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.VARINT);
        writePosition = WriteVarInt64(ref buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteDoubleWithTag(ref byte[] buffer, int writePosition, int fieldNumber, double value)
    {
        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.I64);
        writePosition = WriteFixed64LittleEndianFormat(ref buffer, writePosition, (ulong)BitConverter.DoubleToInt64Bits(value));

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(ref byte[] buffer, int writePosition, int fieldNumber, string value)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        int numberOfUtf8CharsInString;
        unsafe
        {
            fixed (char* strPtr = value)
            {
                numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(strPtr, value.Length);
            }
        }
#else
        int numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(value);
#endif

        writePosition = WriteTag(ref buffer, writePosition, fieldNumber, WireType.LEN);
        writePosition = WriteLength(ref buffer, writePosition, numberOfUtf8CharsInString);
        EnsureBufferCapacity(ref buffer, writePosition + numberOfUtf8CharsInString);

#if NETFRAMEWORK || NETSTANDARD2_0
        _ = Utf8Encoding.GetBytes(value, 0, value.Length, buffer, writePosition);
#else
        _ = Encoding.UTF8.GetBytes(value, buffer.AsSpan().Slice(writePosition));
#endif
        writePosition += numberOfUtf8CharsInString;
        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteSingleByte(ref byte[] buffer, int writePosition, byte value)
    {
        if (buffer.Length == writePosition)
        {
            if (!IncreaseBufferSize(ref buffer))
            {
                // TODO: throw an exception to indicate that the buffer is too large.
            }
        }

        buffer[writePosition++] = value;

        return writePosition;
    }

    internal static bool IncreaseBufferSize(ref byte[] buffer)
    {
        var newBufferSize = buffer.Length * 2;

        if (newBufferSize > 100 * 1024 * 1024)
        {
            return false;
        }

        var newBuffer = new byte[newBufferSize];
        buffer.CopyTo(newBuffer, 0);
        buffer = newBuffer;

        return true;
    }

    internal static byte[] EnsureBufferCapacity(ref byte[] buffer, int requiredSize)
    {
        while (requiredSize > buffer.Length)
        {
            if (!IncreaseBufferSize(ref buffer))
            {
                // TODO: throw an exception to indicate that the buffer is too large.
            }
        }

        return buffer;
    }
}

