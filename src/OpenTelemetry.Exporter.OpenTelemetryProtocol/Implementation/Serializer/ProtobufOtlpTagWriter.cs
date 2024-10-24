// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal sealed class ProtobufOtlpTagWriter : TagWriter<ProtobufOtlpTagWriter.OtlpTagWriterState, ProtobufOtlpTagWriter.OtlpTagWriterArrayState>
{
    private ProtobufOtlpTagWriter()
        : base(new OtlpArrayTagWriter())
    {
    }

    public static ProtobufOtlpTagWriter Instance { get; } = new();

    protected override void WriteIntegralTag(ref OtlpTagWriterState state, string key, long value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        var size = ComputeVarInt64Size((ulong)value) + 1; // ComputeVarint64Size(ulong) + TagSize
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, ProtobufOtlpFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Int_Value, (ulong)value);
    }

    protected override void WriteFloatingPointTag(ref OtlpTagWriterState state, string key, double value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, ProtobufOtlpFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 8 + TagSize
        state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Double_Value, value);
    }

    protected override void WriteBooleanTag(ref OtlpTagWriterState state, string key, bool value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, ProtobufOtlpFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 1 + TagSize
        state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Bool_Value, value);
    }

    protected override void WriteStringTag(ref OtlpTagWriterState state, string key, ReadOnlySpan<char> value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);

        // length = numberOfUtf8CharsInString + tagSize + length field size.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 2, ProtobufOtlpFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
    }

    protected override void WriteArrayTag(ref OtlpTagWriterState state, string key, ref OtlpTagWriterArrayState value)
    {
        // TODO: Expand OtlpTagWriterArrayState.Buffer on IndexOutOfRangeException.
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag and length
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition + 2, ProtobufOtlpFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // Array content length + Array tag size + length field size

        // Write Array tag and length
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Array_Value, ProtobufWireType.LEN);
        Buffer.BlockCopy(value.Buffer, 0, state.Buffer, state.WritePosition, value.WritePosition);
        state.WritePosition += value.WritePosition;
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName) => OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);

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
    private static int ComputeVarInt64Size(ulong value)
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

    internal struct OtlpTagWriterState
    {
        public byte[] Buffer;
        public int WritePosition;
    }

    internal struct OtlpTagWriterArrayState
    {
        public byte[] Buffer;
        public int WritePosition;
    }

    private sealed class OtlpArrayTagWriter : ArrayTagWriter<OtlpTagWriterArrayState>
    {
        [ThreadStatic]
        private static byte[]? threadBuffer;

        public override OtlpTagWriterArrayState BeginWriteArray()
        {
            threadBuffer ??= new byte[2048];

            return new OtlpTagWriterArrayState
            {
                Buffer = threadBuffer,
                WritePosition = 0,
            };
        }

        public override void WriteNullValue(ref OtlpTagWriterArrayState state)
        {
        }

        public override void WriteIntegralValue(ref OtlpTagWriterArrayState state, long value)
        {
            var size = ComputeVarInt64Size((ulong)value) + 1;
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, ProtobufOtlpFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Int_Value, (ulong)value);
        }

        public override void WriteFloatingPointValue(ref OtlpTagWriterArrayState state, double value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, ProtobufOtlpFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Double_Value, value);
        }

        public override void WriteBooleanValue(ref OtlpTagWriterArrayState state, bool value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, ProtobufOtlpFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_Bool_Value, value);
        }

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, ReadOnlySpan<char> value)
        {
            // Write KeyValue.Value tag, length and value.
            var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);

            // length = numberOfUtf8CharsInString + tagSize + length field size.
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 2, ProtobufOtlpFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
        }

        public override void EndWriteArray(ref OtlpTagWriterArrayState state)
        {
        }
    }
}
