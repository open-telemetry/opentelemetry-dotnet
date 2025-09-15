// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NETFRAMEWORK || NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif
using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufSerializer
{
    private const int MaxBufferSize = 100 * 1024 * 1024;
    private const uint UInt128 = 0x80;
    private const ulong ULong128 = 0x80;
    private const int Fixed32Size = 4;
    private const int Fixed64Size = 8;
    private const int MaskBitsLow = 0b_0111_1111;
    private const int MaskBitHigh = 0b_1000_0000;

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
        var slice = buffer.AsSpan(writePosition, 4);
        slice[0] = (byte)((length & MaskBitsLow) | MaskBitHigh);
        slice[1] = (byte)(((length >> 7) & MaskBitsLow) | MaskBitHigh);
        slice[2] = (byte)(((length >> 14) & MaskBitsLow) | MaskBitHigh);
        slice[3] = (byte)((length >> 21) & MaskBitsLow);
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
    internal static int WriteSInt32WithTag(byte[] buffer, int writePosition, int fieldNumber, int value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.VARINT);

        // https://protobuf.dev/programming-guides/encoding/#signed-ints
        writePosition = WriteVarInt32(buffer, writePosition, (uint)((value << 1) ^ (value >> 31)));

        return writePosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteVarInt32(byte[] buffer, int writePosition, uint value)
    {
        while (value >= UInt128)
        {
            buffer[writePosition++] = (byte)(MaskBitHigh | (value & MaskBitsLow));
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
            buffer[writePosition++] = (byte)(MaskBitHigh | (value & MaskBitsLow));
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

    /// <summary>
    /// Computes the number of bytes required to encode a 64-bit unsigned integer in Protocol Buffers' varint format.
    /// </summary>
    /// <remarks>
    /// Protocol Buffers uses variable-length encoding (varint) to serialize integers efficiently:
    /// - Each byte uses 7 bits to encode the number and 1 bit (MSB) to indicate if more bytes follow
    /// - The algorithm checks how many significant bits the number contains by shifting and masking
    /// - Numbers are encoded in groups of 7 bits, from least to most significant
    /// - Each group requires one byte, so the method returns the number of 7-bit groups needed
    ///
    /// Examples:
    /// - Values 0-127 (7 bits) require 1 byte
    /// - Values 128-16383 (14 bits) require 2 bytes
    /// - Values 16384-2097151 (21 bits) require 3 bytes
    /// And so on...
    ///
    /// For more details, see:
    /// - Protocol Buffers encoding reference: https://developers.google.com/protocol-buffers/docs/encoding#varints.
    /// </remarks>
    /// <param name="value">The unsigned 64-bit integer to be encoded.</param>
    /// <returns>Number of bytes needed to encode the value.</returns>
    internal static int ComputeVarInt64Size(ulong value)
    {
        if ((value & (0xffffffffffffffffL << 7)) == 0)
        {
            return 1;
        }

        if ((value & (0xffffffffffffffffL << 14)) == 0)
        {
            return 2;
        }

        if ((value & (0xffffffffffffffffL << 21)) == 0)
        {
            return 3;
        }

        if ((value & (0xffffffffffffffffL << 28)) == 0)
        {
            return 4;
        }

        if ((value & (0xffffffffffffffffL << 35)) == 0)
        {
            return 5;
        }

        if ((value & (0xffffffffffffffffL << 42)) == 0)
        {
            return 6;
        }

        if ((value & (0xffffffffffffffffL << 49)) == 0)
        {
            return 7;
        }

        if ((value & (0xffffffffffffffffL << 56)) == 0)
        {
            return 8;
        }

        if ((value & (0xffffffffffffffffL << 63)) == 0)
        {
            return 9;
        }

        return 10;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(byte[] buffer, int writePosition, int fieldNumber, string value)
    {
        Debug.Assert(value != null, "value was null");

        return WriteStringWithTag(buffer, writePosition, fieldNumber, value.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetNumberOfUtf8CharsInString(ReadOnlySpan<char> value)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        int numberOfUtf8CharsInString;
        unsafe
        {
            fixed (char* strPtr = &GetNonNullPinnableReference(value))
            {
                numberOfUtf8CharsInString = Utf8Encoding.GetByteCount(strPtr, value.Length);
            }
        }
#else
        int numberOfUtf8CharsInString = Utf8Encoding.GetByteCount(value);
#endif
        return numberOfUtf8CharsInString;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(byte[] buffer, int writePosition, int fieldNumber, ReadOnlySpan<char> value)
    {
        var numberOfUtf8CharsInString = GetNumberOfUtf8CharsInString(value);
        return WriteStringWithTag(buffer, writePosition, fieldNumber, numberOfUtf8CharsInString, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteStringWithTag(byte[] buffer, int writePosition, int fieldNumber, int numberOfUtf8CharsInString, ReadOnlySpan<char> value)
    {
        writePosition = WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        writePosition = WriteLength(buffer, writePosition, numberOfUtf8CharsInString);

#if NETFRAMEWORK || NETSTANDARD2_0
        if (buffer.Length - writePosition < numberOfUtf8CharsInString)
        {
            // Note: Validate there is enough space in the buffer to hold the
            // string otherwise throw to trigger a resize of the buffer.
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new IndexOutOfRangeException();
#pragma warning restore CA2201 // Do not raise reserved exception types
        }

        unsafe
        {
            fixed (char* strPtr = &GetNonNullPinnableReference(value))
            {
                fixed (byte* bufferPtr = buffer)
                {
                    var bytesWritten = Utf8Encoding.GetBytes(strPtr, value.Length, bufferPtr + writePosition, numberOfUtf8CharsInString);
                    Debug.Assert(bytesWritten == numberOfUtf8CharsInString, "bytesWritten did not match numberOfUtf8CharsInString");
                }
            }
        }
#else
        var bytesWritten = Utf8Encoding.GetBytes(value, buffer.AsSpan(writePosition));
        Debug.Assert(bytesWritten == numberOfUtf8CharsInString, "bytesWritten did not match numberOfUtf8CharsInString");
#endif

        writePosition += numberOfUtf8CharsInString;
        return writePosition;
    }

    internal static bool IncreaseBufferSize(ref byte[] buffer, OtlpSignalType otlpSignalType)
    {
        if (buffer.Length >= MaxBufferSize)
        {
            OpenTelemetryProtocolExporterEventSource.Log.BufferExceededMaxSize(otlpSignalType.ToString(), buffer.Length);
            return false;
        }

        try
        {
            var newBufferSize = buffer.Length * 2;
            buffer = new byte[newBufferSize];
            return true;
        }
        catch (OutOfMemoryException)
        {
            OpenTelemetryProtocolExporterEventSource.Log.BufferResizeFailedDueToMemory(otlpSignalType.ToString());
            return false;
        }
    }

#if NETFRAMEWORK || NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ref T GetNonNullPinnableReference<T>(ReadOnlySpan<T> span)
        => ref (span.Length != 0) ? ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)) : ref Unsafe.AsRef<T>((void*)1);
#endif
}
