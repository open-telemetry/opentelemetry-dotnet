// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
#if NET
using System.Globalization;
using System.Text;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal sealed class ProtobufOtlpTagWriter : TagWriter<ProtobufOtlpTagWriter.OtlpTagWriterState, ProtobufOtlpTagWriter.OtlpTagWriterArrayState>
{
    private const int ReserveSizeForLength = 4;

    private readonly OtlpArrayTagWriter arrayTagWriter;

    internal ProtobufOtlpTagWriter(OtlpArrayTagWriter arrayTagWriter)
        : base(arrayTagWriter)
    {
        this.arrayTagWriter = arrayTagWriter;
    }

    private ProtobufOtlpTagWriter()
        : this(new OtlpArrayTagWriter())
    {
    }

    public static ProtobufOtlpTagWriter Instance { get; } = new();

    /// <summary>
    /// Writes a tag as a length-delimited <c>KeyValue</c> message under the given field
    /// number.
    /// </summary>
    /// <remarks>
    /// A <c>KeyValue</c> holding a single attribute is almost always well under 128 bytes,
    /// so its length prefix is reserved as a single byte rather than padded out to four.
    /// Attributes are by far the most numerous nested message in a payload, so the three
    /// bytes each would otherwise waste add up to a significant share of it.
    /// </remarks>
    /// <param name="state">The writer state, whose write position is advanced.</param>
    /// <param name="fieldNumber">The field number to write the <c>KeyValue</c> under.</param>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value.</param>
    /// <param name="tagValueMaxLength">The maximum length to write a string value as, if any.</param>
    /// <returns><see langword="true"/> if the key-value pair was successfully written; otherwise <see langword="false"/>.</returns>
    public static bool WriteKeyValue(
        ref OtlpTagWriterState state,
        int fieldNumber,
        string key,
        object? value,
        int? tagValueMaxLength = null)
    {
        var startPosition = state.WritePosition;

        state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, fieldNumber, ProtobufWireType.LEN);

        var lengthPosition = state.WritePosition;
        state.WritePosition += ProtobufSerializer.ReserveSizeForCompactLength;

        if (!Instance.TryWriteTag(ref state, key, value, tagValueMaxLength))
        {
            RollBackTo(ref state, startPosition);
            return false;
        }

        state.WritePosition = ProtobufSerializer.WriteCompactLength(state.Buffer, lengthPosition, state.WritePosition);

        return true;
    }

    protected override void WriteIntegralTag(ref OtlpTagWriterState state, string key, long value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        var size = ProtobufSerializer.ComputeVarInt64Size((ulong)value) + 1; // ComputeVarint64Size(ulong) + TagSize
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Int_Value, (ulong)value);
    }

    protected override void WriteFloatingPointTag(ref OtlpTagWriterState state, string key, double value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 8 + TagSize
        state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Double_Value, value);
    }

    protected override void WriteBooleanTag(ref OtlpTagWriterState state, string key, bool value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 1 + TagSize
        state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Bool_Value, value);
    }

    protected override void WriteStringTag(ref OtlpTagWriterState state, string key, ReadOnlySpan<char> value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value as an AnyValue wrapping the string.
        state.WritePosition = ProtobufSerializer.WriteNestedStringWithTag(
            state.Buffer,
            state.WritePosition,
            ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value,
            ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value,
            value);
    }

    protected override void WriteStringTag(ref OtlpTagWriterState state, string key, string value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value as an AnyValue wrapping the string.
        state.WritePosition = ProtobufSerializer.WriteNestedStringWithTag(
            state.Buffer,
            state.WritePosition,
            ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value,
            ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value,
            value);
    }

#if NET
    protected override bool TryWriteUtf8SpanFormattableTag(ref OtlpTagWriterState state, string key, IUtf8SpanFormattable value, int? tagValueMaxLength)
    {
        // A UTF-16 char is at most 3 UTF-8 bytes
        const int MaxUtf8BytesPerChar = 3;

        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        var anyValueLengthPosition = state.WritePosition;
        state.WritePosition += ReserveSizeForLength;

        state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value, ProtobufWireType.LEN);
        var stringLengthPosition = state.WritePosition;
        state.WritePosition += ReserveSizeForLength;

        if (tagValueMaxLength == 0)
        {
            EndUtf8StringTag(ref state, anyValueLengthPosition, stringLengthPosition, 0);
            return true;
        }

        var buffer = state.Buffer.AsSpan(state.WritePosition);

        // If max attribute length is in place, we'll cap the buffer size to avoid very large values exhausting the buffer
        // If the buffer isn't large enough to contain the max possible truncated output, we'll pass it as-is and grow it if the write fails
        var isBufferCapped = false;
        if (tagValueMaxLength is { } maxLength && buffer.Length >= (long)maxLength * MaxUtf8BytesPerChar)
        {
            buffer = buffer[..(maxLength * MaxUtf8BytesPerChar)];
            isBufferCapped = true;
        }

        try
        {
            if (value.TryFormat(buffer, out var bytesWritten, format: default, CultureInfo.InvariantCulture))
            {
                var truncatedLength = TruncateUtf8String(buffer[..bytesWritten], tagValueMaxLength).Length;
                buffer[truncatedLength..bytesWritten].Clear();
                EndUtf8StringTag(ref state, anyValueLengthPosition, stringLengthPosition, truncatedLength);
                return true;
            }
        }
        catch (Exception)
        {
            // How much the formatter wrote is unknown, and the buffer may be returned to a shared pool.
            buffer.Clear();
            return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
        }

        if (!isBufferCapped)
        {
            throw new ArgumentOutOfRangeException(nameof(state), "The buffer is too small to hold the data being written.");
        }

        // The failed TryFormat may have filled the buffer beyond what the shorter fallback overwrites.
        // Not needed before the throw above: callers that grow the buffer clear the whole of the old one.
        buffer.Clear();

        try
        {
            // We don't want to grow the buffer indefinitely- just convert to string and truncate this
            var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (stringValue == null)
            {
                return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
            }

            var bytesWritten = Encoding.UTF8.GetBytes(TruncateString(stringValue.AsSpan(), tagValueMaxLength), buffer);
            EndUtf8StringTag(ref state, anyValueLengthPosition, stringLengthPosition, bytesWritten);
            return true;
        }
        catch (Exception)
        {
            return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
        }
    }
#endif

    protected override void WriteArrayTag(ref OtlpTagWriterState state, string key, ref OtlpTagWriterArrayState value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag and length
        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)value.WritePosition);
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition + 1 + serializedLengthSize, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // Array content length + Array tag size + length field size

        // Write Array tag and length
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Array_Value, ProtobufWireType.LEN);
        Buffer.BlockCopy(value.Buffer, 0, state.Buffer, state.WritePosition, value.WritePosition);
        state.WritePosition += value.WritePosition;

        // The scratch buffer contents have now been copied into the main buffer,
        // so return it to the pool. A fresh buffer is rented on the next array.
        this.arrayTagWriter.ReleaseBuffer(ref value);
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName) => OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);

    protected override bool TryWriteEmptyTag(ref OtlpTagWriterState state, string key, object? value)
    {
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 0, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        return true;
    }

    protected override bool TryWriteByteArrayTag(ref OtlpTagWriterState state, string key, ReadOnlySpan<byte> value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)value.Length);

        // length = value.Length + tagSize + length field size.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.Length + 1 + serializedLengthSize, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteByteArrayWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Bytes_Value, value);

        return true;
    }

    protected override void WriteKvListTag(ref OtlpTagWriterState state, string key, IEnumerable<KeyValuePair<string, object?>> kvList, int? tagValueMaxLength)
    {
        var startPosition = state.WritePosition;

        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Key, key);

        // Reserve space for KeyValue.value (AnyValue wrapper)
        state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        int anyValueLengthPos = state.WritePosition;
        state.WritePosition += ReserveSizeForLength;

        // Reserve space for AnyValue.kvlist_value (KeyValueList wrapper)
        state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Kvlist_Value, ProtobufWireType.LEN);
        int kvlistLengthPos = state.WritePosition;
        state.WritePosition += ReserveSizeForLength;

        try
        {
            foreach (var kvp in kvList)
            {
                var startEntryPosition = state.WritePosition;

                // Write KeyValueList.values tag (field 1, LEN) + reserve length per entry
                state.WritePosition = ProtobufSerializer.WriteTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.KeyValueList_Values, ProtobufWireType.LEN);
                int entryLengthPos = state.WritePosition;
                state.WritePosition += ReserveSizeForLength;

                // Drop a tag if we fail to write it, and reset the position to the start of the entry to overwrite the reserved tag and length for the next entry.
                if (!this.TryWriteTag(ref state, kvp.Key, kvp.Value, tagValueMaxLength))
                {
                    RollBackTo(ref state, startEntryPosition);
                    continue;
                }

                ProtobufSerializer.WriteReservedLength(state.Buffer, entryLengthPos, state.WritePosition - (entryLengthPos + ReserveSizeForLength));
            }
        }
        catch (Exception ex) when (ex is not (IndexOutOfRangeException or ArgumentException))
        {
            RollBackTo(ref state, startPosition);

            throw;
        }

        // Fill in reserved lengths
        ProtobufSerializer.WriteReservedLength(state.Buffer, kvlistLengthPos, state.WritePosition - (kvlistLengthPos + ReserveSizeForLength));
        ProtobufSerializer.WriteReservedLength(state.Buffer, anyValueLengthPos, state.WritePosition - (anyValueLengthPos + ReserveSizeForLength));
    }

#if NET
    private static void EndUtf8StringTag(ref OtlpTagWriterState state, int anyValueLengthPosition, int stringLengthPosition, int utf8Length)
    {
        state.WritePosition += utf8Length;

        ProtobufSerializer.WriteReservedLength(state.Buffer, stringLengthPosition, utf8Length);
        ProtobufSerializer.WriteReservedLength(state.Buffer, anyValueLengthPosition, state.WritePosition - (anyValueLengthPosition + ReserveSizeForLength));
    }

    // Truncates on whole code points, so a UTF-8 sequence or surrogate pair is never split.
    private static ReadOnlySpan<byte> TruncateUtf8String(ReadOnlySpan<byte> value, int? maxLength)
    {
        if (!maxLength.HasValue || value.Length <= maxLength)
        {
            return value;
        }

        int bytes = 0, chars = 0;

        while (bytes < value.Length)
        {
            Rune.DecodeFromUtf8(value[bytes..], out var rune, out var consumed);
            if (chars + rune.Utf16SequenceLength > maxLength)
            {
                break;
            }

            chars += rune.Utf16SequenceLength;
            bytes += consumed;
        }

        return value[..bytes];
    }
#endif

    private static void RollBackTo(ref OtlpTagWriterState state, int startPosition)
    {
        // Reserving a length can advance WritePosition past the end of the buffer.
        state.Buffer.AsSpan(startPosition, Math.Min(state.WritePosition, state.Buffer.Length) - startPosition).Clear();
        state.WritePosition = startPosition;
    }

    internal struct OtlpTagWriterState
    {
        public byte[] Buffer;
        public int DroppedTagCount;
        public int TagCount;
        public int WritePosition;
    }

    internal struct OtlpTagWriterArrayState
    {
        public byte[] Buffer;
        public int WritePosition;
    }

    internal sealed class OtlpArrayTagWriter : ArrayTagWriter<OtlpTagWriterArrayState>
    {
        private const int DefaultBufferSize = 2048;
        private const int MaxBufferSize = 2 * 1024 * 1024;

        private readonly ArrayPool<byte> pool;

        public OtlpArrayTagWriter()
            : this(ArrayPool<byte>.Shared)
        {
        }

        internal OtlpArrayTagWriter(ArrayPool<byte> pool)
        {
            Guard.ThrowIfNull(pool);
            this.pool = pool;
        }

        public override OtlpTagWriterArrayState BeginWriteArray()
            => new()
            {
                Buffer = this.pool.Rent(DefaultBufferSize),
                WritePosition = 0,
            };

        public override void WriteNullValue(ref OtlpTagWriterArrayState state)
            => state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 0, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);

        public override void WriteIntegralValue(ref OtlpTagWriterArrayState state, long value)
        {
            var size = ProtobufSerializer.ComputeVarInt64Size((ulong)value) + 1;
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Int_Value, (ulong)value);
        }

        public override void WriteFloatingPointValue(ref OtlpTagWriterArrayState state, double value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Double_Value, value);
        }

        public override void WriteBooleanValue(ref OtlpTagWriterArrayState state, bool value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_Bool_Value, value);
        }

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, ReadOnlySpan<char> value) =>
            state.WritePosition = ProtobufSerializer.WriteNestedStringWithTag(
                state.Buffer,
                state.WritePosition,
                ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value,
                ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value,
                value);

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, string value)
            => state.WritePosition = ProtobufSerializer.WriteNestedStringWithTag(
                state.Buffer,
                state.WritePosition,
                ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value,
                ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value,
                value);

        public override void EndWriteArray(ref OtlpTagWriterArrayState state)
        {
        }

        public override void AbortWriteArray(ref OtlpTagWriterArrayState state)
            => this.ReleaseBuffer(ref state);

        public override bool TryResize(ref OtlpTagWriterArrayState state)
        {
            var smallerBuffer = state.Buffer;

            if (smallerBuffer.Length >= MaxBufferSize)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ArrayBufferExceededMaxSize();
                return false;
            }

            byte[] largerBuffer;
            try
            {
                largerBuffer = this.pool.Rent(smallerBuffer.Length * 2);
            }
            catch (OutOfMemoryException)
            {
                OpenTelemetryProtocolExporterEventSource.Log.BufferResizeFailedDueToMemory(nameof(OtlpArrayTagWriter));
                return false;
            }

            // Swap in the larger buffer first, then return the smaller one for reuse.
            state.Buffer = largerBuffer;
            state.WritePosition = 0;
            ProtobufSerializer.ReturnBuffer(this.pool, smallerBuffer);

            return true;
        }

        internal void ReleaseBuffer(ref OtlpTagWriterArrayState state)
        {
            var buffer = state.Buffer;
            state = default;

            if (buffer != null)
            {
                ProtobufSerializer.ReturnBuffer(this.pool, buffer);
            }
        }
    }
}
