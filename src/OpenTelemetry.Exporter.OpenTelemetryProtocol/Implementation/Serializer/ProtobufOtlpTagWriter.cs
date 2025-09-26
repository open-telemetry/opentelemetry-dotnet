// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

        // Write KeyValue.Value tag, length and value.
        var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);
        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

        // length = numberOfUtf8CharsInString + tagSize + length field size.
        state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 1 + serializedLengthSize, ProtobufOtlpCommonFieldNumberConstants.KeyValue_Value, ProtobufWireType.LEN);
        state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
    }

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
        [ThreadStatic]
        internal static byte[]? ThreadBuffer;
        private const int MaxBufferSize = 2 * 1024 * 1024;

        public override OtlpTagWriterArrayState BeginWriteArray()
        {
            ThreadBuffer ??= new byte[2048];

            return new OtlpTagWriterArrayState
            {
                Buffer = ThreadBuffer,
                WritePosition = 0,
            };
        }

        public override void WriteNullValue(ref OtlpTagWriterArrayState state)
        {
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, 0, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
        }

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

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, ReadOnlySpan<char> value)
        {
            // Write KeyValue.Value tag, length and value.
            var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);
            var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

            // length = numberOfUtf8CharsInString + tagSize + length field size.
            state.WritePosition = ProtobufSerializer.WriteTagAndLength(state.Buffer, state.WritePosition, numberOfUtf8CharsInString + 1 + serializedLengthSize, ProtobufOtlpCommonFieldNumberConstants.ArrayValue_Value, ProtobufWireType.LEN);
            state.WritePosition = ProtobufSerializer.WriteStringWithTag(state.Buffer, state.WritePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
        }

        public override void EndWriteArray(ref OtlpTagWriterArrayState state)
        {
        }

        public override bool TryResize()
        {
            var buffer = ThreadBuffer;

            Debug.Assert(buffer != null, "buffer was null");

            if (buffer!.Length >= MaxBufferSize)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ArrayBufferExceededMaxSize();
                return false;
            }

            try
            {
                ThreadBuffer = new byte[buffer.Length * 2];
                return true;
            }
            catch (OutOfMemoryException)
            {
                OpenTelemetryProtocolExporterEventSource.Log.BufferResizeFailedDueToMemory(nameof(OtlpArrayTagWriter));
                return false;
            }
        }
    }
}
