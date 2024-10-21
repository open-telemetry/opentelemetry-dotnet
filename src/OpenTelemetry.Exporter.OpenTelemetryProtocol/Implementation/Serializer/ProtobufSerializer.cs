// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufSerializer
{
    private const uint UInt128 = 0x80;
    private const ulong ULong128 = 0x80;
    private const int Fixed32Size = 4;
    private const int Fixed64Size = 8;

    private static readonly Encoding Utf8Encoding = Encoding.UTF8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint GetTagValue(int fieldNumber, ProtobufWireType wireType) => ((uint)(fieldNumber << 3)) | (uint)wireType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTag(byte[] buffer, int writePosition, int fieldNumber, ProtobufWireType type) => WriteVarInt32(buffer, writePosition, GetTagValue(fieldNumber, type));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLength(byte[] buffer, int writePosition, int length) => WriteVarInt32(buffer, writePosition, (uint)length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteTagAndLength(byte[] buffer, int writePosition, int contentLength, int fieldNumber, ProtobufWireType type)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, type);
        writePosition = WriteLength(buffer, writePosition, contentLength);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteReservedLength(byte[] buffer, int writePosition, int length)
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
    internal static int WriteBoolWithTag(byte[] buffer, int writePosition, int fieldNumber, bool value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.VARINT);
        buffer[writePosition++] = value ? (byte)1 : (byte)0;
        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteEnumWithTag(byte[] buffer, int writePosition, int fieldNumber, int value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.VARINT);
        buffer[writePosition++] = (byte)value;
        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32LittleEndianFormat(byte[] buffer, int writePosition, uint value)
    {
        Span<byte> span = new(buffer, writePosition, Fixed32Size);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        writePosition += Fixed32Size;

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64LittleEndianFormat(byte[] buffer, int writePosition, ulong value)
    {
        Span<byte> span = new(buffer, writePosition, Fixed64Size);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        writePosition += Fixed64Size;

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed32WithTag(byte[] buffer, int writePosition, int fieldNumber, uint value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.I32);
        writePosition = WriteFixed32LittleEndianFormat(buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteFixed64WithTag(byte[] buffer, int writePosition, int fieldNumber, ulong value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.I64);
        writePosition = WriteFixed64LittleEndianFormat(buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarInt32(byte[] buffer, int writePosition, uint value)
    {
        while (value >= UInt128)
        {
            buffer[writePosition++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        buffer[writePosition++] = (byte)value;
        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarInt64(byte[] buffer, int writePosition, ulong value)
    {
        while (value >= ULong128)
        {
            buffer[writePosition++] = (byte)(0x80 | (value & 0x7F));
            value >>= 7;
        }

        buffer[writePosition++] = (byte)value;
        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteInt64WithTag(byte[] buffer, int writePosition, int fieldNumber, ulong value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.VARINT);
        writePosition = WriteVarInt64(buffer, writePosition, value);

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteDoubleWithTag(byte[] buffer, int writePosition, int fieldNumber, double value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.I64);
        writePosition = WriteFixed64LittleEndianFormat(buffer, writePosition, (ulong)BitConverter.DoubleToInt64Bits(value));

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(byte[] buffer, int writePosition, int fieldNumber, string value)
    {
        Debug.Assert(value != null, "value was null");

        return WriteStringWithTag(buffer, writePosition, fieldNumber, value.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(byte[] buffer, int writePosition, int fieldNumber, ReadOnlySpan<char> value)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        int numberOfUtf8CharsInString;
        unsafe
        {
            fixed (char* strPtr = value)
            {
                numberOfUtf8CharsInString = Utf8Encoding.GetByteCount(strPtr, value.Length);
            }
        }
#else
        int numberOfUtf8CharsInString = Utf8Encoding.GetByteCount(value);
#endif

        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        writePosition = WriteLength(buffer, writePosition, numberOfUtf8CharsInString);

#if NETFRAMEWORK || NETSTANDARD2_0
        unsafe
        {
            fixed (char* strPtr = value)
            {
                fixed (byte* bufferPtr = buffer)
                {
                    var bytesWritten = Utf8Encoding.GetBytes(strPtr, value.Length, bufferPtr + writePosition, numberOfUtf8CharsInString);
                    Debug.Assert(bytesWritten == numberOfUtf8CharsInString, "bytesWritten did not match numberOfUtf8CharsInString");
                }
            }
        }
#else
        var bytesWritten = Utf8Encoding.GetBytes(value, buffer.AsSpan().Slice(writePosition));
        Debug.Assert(bytesWritten == numberOfUtf8CharsInString, "bytesWritten did not match numberOfUtf8CharsInString");
#endif

        writePosition += numberOfUtf8CharsInString;
        return writePosition;
    }
}
