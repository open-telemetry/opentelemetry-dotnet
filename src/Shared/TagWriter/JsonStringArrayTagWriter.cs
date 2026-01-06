// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;

namespace OpenTelemetry.Internal;

internal abstract class JsonStringArrayTagWriter<TTagState> : TagWriter<TTagState, JsonStringArrayTagWriter<TTagState>.JsonArrayTagWriterState, JsonStringArrayTagWriter<TTagState>.JsonKvlistTagWriterState>
    where TTagState : notnull
{
    protected JsonStringArrayTagWriter()
        : base(new JsonArrayTagWriter(), kvlistTagWriter: null)
    {
    }

    protected sealed override void WriteArrayTag(ref TTagState writer, string key, ref JsonArrayTagWriterState array)
    {
        var result = array.Stream.TryGetBuffer(out var buffer);

        Debug.Assert(result, "result was false");

        this.WriteArrayTag(ref writer, key, buffer);
    }

    protected abstract void WriteArrayTag(ref TTagState writer, string key, ArraySegment<byte> arrayUtf8JsonBytes);

    protected override bool TryWriteByteArrayTag(ref TTagState consoleTag, string key, ReadOnlySpan<byte> value) => false;

    internal readonly struct JsonArrayTagWriterState(MemoryStream stream, Utf8JsonWriter writer)
    {
        public MemoryStream Stream { get; } = stream;

        public Utf8JsonWriter Writer { get; } = writer;
    }

    // Placeholder struct - JSON writer doesn't support kvlist
    internal readonly struct JsonKvlistTagWriterState
    {
    }

    internal sealed class JsonArrayTagWriter : ArrayTagWriter<JsonArrayTagWriterState>
    {
        [ThreadStatic]
        private static MemoryStream? threadStream;

        [ThreadStatic]
        private static Utf8JsonWriter? threadWriter;

        public override JsonArrayTagWriterState BeginWriteArray()
        {
            var state = EnsureWriter();
            state.Writer.WriteStartArray();
            return state;
        }

        public override void EndWriteArray(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteEndArray();
            state.Writer.Flush();
        }

        public override void WriteBooleanValue(ref JsonArrayTagWriterState state, bool value)
        {
            state.Writer.WriteBooleanValue(value);
        }

        public override void WriteFloatingPointValue(ref JsonArrayTagWriterState state, double value)
        {
            state.Writer.WriteNumberValue(value);
        }

        public override void WriteIntegralValue(ref JsonArrayTagWriterState state, long value)
        {
            state.Writer.WriteNumberValue(value);
        }

        public override void WriteNullValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteNullValue();
        }

        public override void WriteStringValue(ref JsonArrayTagWriterState state, ReadOnlySpan<char> value)
        {
            state.Writer.WriteStringValue(value);
        }

        private static JsonArrayTagWriterState EnsureWriter()
        {
            if (threadStream == null)
            {
                threadStream = new MemoryStream();
                threadWriter = new Utf8JsonWriter(threadStream);
                return new(threadStream, threadWriter);
            }
            else
            {
                threadStream.SetLength(0);
                threadWriter!.Reset(threadStream);
                return new(threadStream, threadWriter);
            }
        }
    }
}
