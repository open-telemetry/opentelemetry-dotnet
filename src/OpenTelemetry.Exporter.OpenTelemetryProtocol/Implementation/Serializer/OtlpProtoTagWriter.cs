// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal sealed class OtlpProtoTagWriter : TagWriter<OtlpProtoTagWriter.OtlpTagWriterState, OtlpProtoTagWriter.OtlpTagWriterArrayState>
{
    private OtlpProtoTagWriter()
        : base(new OtlpArrayTagWriter())
    {
    }

    public static OtlpProtoTagWriter Instance { get; } = new();

    protected override void WriteIntegralTag(ref OtlpTagWriterState state, string key, long value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        var size = ComputeVarint64Size((ulong)value) + 1; // ComputeVarint64Size(ulong) + TagSize
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, FieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Int_Value, (ulong)value);
    }

    protected override void WriteFloatingPointTag(ref OtlpTagWriterState state, string key, double value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, FieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 8 + TagSize
        state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Double_Value, value);
    }

    protected override void WriteBooleanTag(ref OtlpTagWriterState state, string key, bool value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, FieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // 1 + TagSize
        state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Bool_Value, value);
    }

    protected override void WriteStringTag(ref OtlpTagWriterState state, string key, ReadOnlySpan<char> value)
    {
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag, length and value.
#if NETFRAMEWORK || NETSTANDARD2_0
        int numberOfUtf8CharsInString;
        unsafe
        {
            fixed (char* p = value)
            {
                numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(p, value.Length);
            }
        }
#else
        int numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(value);
#endif

        // length = numberOfUtf8CharsInString + tagSize + length field size.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 2, FieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_String_Value, value);
    }

    protected override void WriteArrayTag(ref OtlpTagWriterState state, string key, ref OtlpTagWriterArrayState value)
    {
        // TODO: Expand OtlpTagWriterArrayState.Buffer on IndexOutOfRangeException.
        // Write KeyValue tag
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.KeyValue_Key, key);

        // Write KeyValue.Value tag and length
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition + 2, FieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN); // Array content length + Array tag size + length field size

        // Write Array tag and length
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, value.WritePosition, FieldNumberConstants.AnyValue_Array_Value, ProtobufWireType.LEN);
        Buffer.BlockCopy(value.Buffer, 0, state.Buffer, state.WritePosition, value.WritePosition);
        state.WritePosition += value.WritePosition;
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName) => OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);

    private static int ComputeVarint64Size(ulong value)
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
            var size = ComputeVarint64Size((ulong)value) + 1;
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, size, FieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteInt64WithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Int_Value, (ulong)value);
        }

        public override void WriteFloatingPointValue(ref OtlpTagWriterArrayState state, double value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 9, FieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteDoubleWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Double_Value, value);
        }

        public override void WriteBooleanValue(ref OtlpTagWriterArrayState state, bool value)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 2, FieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteBoolWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_Bool_Value, value);
        }

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, ReadOnlySpan<char> value)
        {
            // Write KeyValue.Value tag, length and value.
#if NETFRAMEWORK || NETSTANDARD2_0
            int numberOfUtf8CharsInString;
            unsafe
            {
                fixed (char* p = value)
                {
                    numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(p, value.Length);
                }
            }
#else
            int numberOfUtf8CharsInString = Encoding.UTF8.GetByteCount(value);
#endif

            // length = numberOfUtf8CharsInString + tagSize + length field size.
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 2, FieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, FieldNumberConstants.AnyValue_String_Value, value);
        }

        public override void EndWriteArray(ref OtlpTagWriterArrayState state)
        {
        }
    }
}
