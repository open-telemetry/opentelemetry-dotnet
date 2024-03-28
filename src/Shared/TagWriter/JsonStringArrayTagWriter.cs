// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Text.Json;

namespace OpenTelemetry.Internal;

internal abstract class JsonStringArrayTagWriter<TTagState> : TagWriter<TTagState, JsonStringArrayTagWriter<TTagState>.JsonArrayTagWriterState>
    where TTagState : notnull
{
    protected JsonStringArrayTagWriter()
        : base(new JsonArrayTagWriter())
    {
    }

    protected sealed override void WriteArrayTag(TTagState writer, string key, JsonStringArrayTagWriter<TTagState>.JsonArrayTagWriterState array)
    {
        var result = array.Stream.TryGetBuffer(out var buffer);

        Debug.Assert(result, "result was false");

        this.WriteArrayTag(writer, key, buffer);
    }

    protected abstract void WriteArrayTag(TTagState writer, string key, ArraySegment<byte> arrayUtf8JsonBytes);

    internal readonly struct JsonArrayTagWriterState(MemoryStream stream, Utf8JsonWriter writer)
    {
        public MemoryStream Stream { get; } = stream;

        public Utf8JsonWriter Writer { get; } = writer;
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

        public override void EndWriteArray(JsonArrayTagWriterState state)
        {
            state.Writer.WriteEndArray();
            state.Writer.Flush();
        }

        public override void WriteBooleanTag(JsonArrayTagWriterState state, bool value)
        {
            state.Writer.WriteBooleanValue(value);
        }

        public override void WriteFloatingPointTag(JsonArrayTagWriterState state, double value)
        {
            state.Writer.WriteNumberValue(value);
        }

        public override void WriteIntegralTag(JsonArrayTagWriterState state, long value)
        {
            state.Writer.WriteNumberValue(value);
        }

        public override void WriteNullTag(JsonArrayTagWriterState state)
        {
            state.Writer.WriteNullValue();
        }

        public override void WriteStringTag(JsonArrayTagWriterState state, string value)
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
