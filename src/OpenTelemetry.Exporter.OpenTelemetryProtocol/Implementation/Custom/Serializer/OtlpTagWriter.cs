// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.Serializer;

internal sealed class OtlpTagWriter : TagWriter<OtlpTagWriter.OtlpTagWriterState, OtlpTagWriter.OtlpTagWriterArrayState>
{
    private OtlpTagWriter()
        : base(new OtlpArrayTagWriter())
    {
    }

    public static OtlpTagWriter Instance { get; } = new();

    protected override void WriteIntegralTag(ref OtlpTagWriterState state, string key, long value)
    {
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.KeyValue_key, key);
        state.Cursor = Writer.WriteTagAndLengthPrefix(ref state.Buffer, state.Cursor, 9, FieldNumberConstants.KeyValue_value, WireType.LEN);
        state.Cursor = Writer.WriteInt64WithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.AnyValue_int_value, (ulong)value);
    }

    protected override void WriteFloatingPointTag(ref OtlpTagWriterState state, string key, double value)
    {
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.KeyValue_key, key);
        state.Cursor = Writer.WriteTagAndLengthPrefix(ref state.Buffer, state.Cursor, 9, FieldNumberConstants.KeyValue_value, WireType.LEN);
        state.Cursor = Writer.WriteDoubleWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.AnyValue_double_value, value);
    }

    protected override void WriteBooleanTag(ref OtlpTagWriterState state, string key, bool value)
    {
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.KeyValue_key, key);
        state.Cursor = Writer.WriteTagAndLengthPrefix(ref state.Buffer, state.Cursor, 2, FieldNumberConstants.KeyValue_value, WireType.LEN);
        state.Cursor = Writer.WriteBoolWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.AnyValue_bool_value, value);
    }

    protected override void WriteStringTag(ref OtlpTagWriterState state, string key, ReadOnlySpan<char> value)
    {
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.KeyValue_key, key);

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
        state.Cursor = Writer.WriteTagAndLengthPrefix(ref state.Buffer, state.Cursor, numberOfUtf8CharsInString + 2, FieldNumberConstants.KeyValue_value, WireType.LEN);
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.AnyValue_string_value, value, numberOfUtf8CharsInString);
    }

    protected override void WriteArrayTag(ref OtlpTagWriterState state, string key, ref OtlpTagWriterArrayState value)
    {
        state.Cursor = Writer.WriteStringWithTag(ref state.Buffer, state.Cursor, FieldNumberConstants.KeyValue_key, key);

        // Write Array tag and length
        state.Cursor = Writer.WriteTagAndLengthPrefix(ref state.Buffer, state.Cursor, value.Cursor, FieldNumberConstants.AnyValue_array_value, WireType.LEN);

        // Copy array bytes to tags buffer.
        // TODO: handle insufficient space.
        Buffer.BlockCopy(value.Buffer, 0, state.Buffer, state.Cursor, value.Cursor);

        // Move the cursor for tags.
        state.Cursor += value.Cursor;
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);
    }

    internal struct OtlpTagWriterState
    {
        public byte[] Buffer;
        public int Cursor;
    }

    internal struct OtlpTagWriterArrayState
    {
        public byte[] Buffer;
        public int Cursor;
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
                Cursor = 0,
            };
        }

        public override void WriteNullValue(ref OtlpTagWriterArrayState state)
        {
            // Do nothing.
        }

        public override void WriteIntegralValue(ref OtlpTagWriterArrayState state, long value)
        {
            // TODO
        }

        public override void WriteFloatingPointValue(ref OtlpTagWriterArrayState state, double value)
        {
            // TODO
        }

        public override void WriteBooleanValue(ref OtlpTagWriterArrayState state, bool value)
        {
            // TODO
        }

        public override void WriteStringValue(ref OtlpTagWriterArrayState state, ReadOnlySpan<char> value)
        {
            // TODO
        }

        public override void EndWriteArray(ref OtlpTagWriterArrayState state)
        {
        }
    }
}
