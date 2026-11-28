// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// A minimal protobuf writer covering the wire types needed for the <c>ProcessContext</c>
/// message. It follows the position based style of the OTLP exporter's serializer:
/// a nested message reserves a single length byte, which is widened (by moving the
/// content) only when the content turns out to be 128 bytes or longer, so the output
/// is always the canonical, shortest encoding.
/// </summary>
internal sealed class ProtobufWriter
{
    private const int MaskBitsLow = 0b_0111_1111;
    private const int MaskBitHigh = 0b_1000_0000;

    private static readonly Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    private byte[] buffer;

    internal ProtobufWriter(int initialCapacity = 256)
    {
        this.buffer = new byte[initialCapacity];
    }

    internal int Position { get; private set; }

    internal static int ComputeVarIntSize(uint value)
    {
        var size = 1;
        while (value > MaskBitsLow)
        {
            value >>= 7;
            size++;
        }

        return size;
    }

    internal byte[] ToArray()
    {
        var result = new byte[this.Position];
        Buffer.BlockCopy(this.buffer, 0, result, 0, this.Position);
        return result;
    }

    /// <summary>
    /// Discards everything written after <paramref name="position"/>.
    /// </summary>
    /// <param name="position">A position previously read from <see cref="Position"/>.</param>
    internal void Rewind(int position)
    {
        Debug.Assert(position >= 0 && position <= this.Position, "position out of range");
        this.Position = position;
    }

    internal void WriteTag(int fieldNumber, ProtobufWireType wireType)
        => this.WriteVarInt((uint)(fieldNumber << 3) | (uint)wireType);

    internal void WriteVarInt(ulong value)
    {
        this.EnsureCapacity(10);
        while (value > MaskBitsLow)
        {
            this.buffer[this.Position++] = (byte)((value & MaskBitsLow) | MaskBitHigh);
            value >>= 7;
        }

        this.buffer[this.Position++] = (byte)value;
    }

    internal void WriteFixed64(ulong value)
    {
        // Fixed width protobuf fields are always little-endian on the wire.
        this.EnsureCapacity(8);
        for (var i = 0; i < 8; i++)
        {
            this.buffer[this.Position++] = (byte)(value >> (8 * i));
        }
    }

    internal void WriteStringField(int fieldNumber, string value)
    {
        // Invalid UTF-16 (a lone surrogate) is replaced with U+FFFD, so the
        // output is always valid UTF-8 as proto3 string fields require.
        var byteCount = Utf8Encoding.GetByteCount(value);
        this.WriteTag(fieldNumber, ProtobufWireType.LEN);
        this.WriteVarInt((uint)byteCount);
        this.EnsureCapacity(byteCount);
        this.Position += Utf8Encoding.GetBytes(value, 0, value.Length, this.buffer, this.Position);
    }

    internal void WriteBytesField(int fieldNumber, byte[] value)
    {
        this.WriteTag(fieldNumber, ProtobufWireType.LEN);
        this.WriteVarInt((uint)value.Length);
        this.EnsureCapacity(value.Length);
        Buffer.BlockCopy(value, 0, this.buffer, this.Position, value.Length);
        this.Position += value.Length;
    }

    /// <summary>
    /// Writes the tag of a nested message and reserves one byte for its length.
    /// </summary>
    /// <param name="fieldNumber">Field number of the nested message.</param>
    /// <returns>The position to pass to <see cref="EndMessage"/>.</returns>
    internal int BeginMessage(int fieldNumber)
    {
        this.WriteTag(fieldNumber, ProtobufWireType.LEN);
        this.EnsureCapacity(1);
        return this.Position++;
    }

    /// <summary>
    /// Fills in the length reserved by <see cref="BeginMessage"/>, shifting the
    /// content along when the length needs more than one byte.
    /// </summary>
    /// <param name="lengthPosition">The value returned by <see cref="BeginMessage"/>.</param>
    internal void EndMessage(int lengthPosition)
    {
        var contentPosition = lengthPosition + 1;
        var contentLength = this.Position - contentPosition;
        Debug.Assert(contentLength >= 0, "contentLength was negative");

        if (contentLength <= MaskBitsLow)
        {
            this.buffer[lengthPosition] = (byte)contentLength;
            return;
        }

        var lengthSize = ComputeVarIntSize((uint)contentLength);
        var shift = lengthSize - 1;
        this.EnsureCapacity(shift);
        Buffer.BlockCopy(this.buffer, contentPosition, this.buffer, contentPosition + shift, contentLength);

        var value = (uint)contentLength;
        var position = lengthPosition;
        while (value > MaskBitsLow)
        {
            this.buffer[position++] = (byte)((value & MaskBitsLow) | MaskBitHigh);
            value >>= 7;
        }

        this.buffer[position] = (byte)value;
        this.Position += shift;
    }

    private void EnsureCapacity(int additional)
    {
        var required = this.Position + additional;
        if (required <= this.buffer.Length)
        {
            return;
        }

        var newSize = Math.Max(this.buffer.Length * 2, required);
        var newBuffer = new byte[newSize];
        Buffer.BlockCopy(this.buffer, 0, newBuffer, 0, this.Position);
        this.buffer = newBuffer;
    }
}
