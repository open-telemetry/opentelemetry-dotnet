// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;

namespace OpenTelemetry.Internal;

internal abstract class JsonStringArrayTagWriter<TTagState> : TagWriter<TTagState, JsonStringArrayTagWriter<TTagState>.JsonArrayTagWriterState, JsonStringArrayTagWriter<TTagState>.JsonKvlistTagWriterState>
    where TTagState : notnull
{
    protected JsonStringArrayTagWriter()
        : base(new JsonArrayTagWriter(), new JsonKvlistTagWriter())
    {
    }

    protected sealed override void WriteArrayTag(ref TTagState writer, string key, ref JsonArrayTagWriterState array)
    {
        var result = array.Stream.TryGetBuffer(out var buffer);

        Debug.Assert(result, "result was false");

        this.WriteArrayTag(ref writer, key, buffer);
    }

    protected sealed override void WriteKvlistTag(ref TTagState writer, string key, ref JsonKvlistTagWriterState kvlist)
    {
        var result = kvlist.Stream.TryGetBuffer(out var buffer);

        Debug.Assert(result, "result was false");

        this.WriteKvlistTag(ref writer, key, buffer);
    }

    protected abstract void WriteArrayTag(ref TTagState writer, string key, ArraySegment<byte> arrayUtf8JsonBytes);

    protected abstract void WriteKvlistTag(ref TTagState writer, string key, ArraySegment<byte> kvlistUtf8JsonBytes);

    protected override bool TryWriteByteArrayTag(ref TTagState consoleTag, string key, ReadOnlySpan<byte> value) => false;

    internal readonly struct JsonArrayTagWriterState(MemoryStream stream, Utf8JsonWriter writer)
    {
        public MemoryStream Stream { get; } = stream;

        public Utf8JsonWriter Writer { get; } = writer;
    }

    internal readonly struct JsonKvlistTagWriterState(MemoryStream stream, Utf8JsonWriter writer, int nestingLevel)
    {
        public MemoryStream Stream { get; } = stream;

        public Utf8JsonWriter Writer { get; } = writer;

        public int NestingLevel { get; } = nestingLevel;
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

    internal sealed class JsonKvlistTagWriter : KvlistTagWriter<JsonKvlistTagWriterState>
    {
        [ThreadStatic]
        private static MemoryStream? threadStream;

        [ThreadStatic]
        private static Utf8JsonWriter? threadWriter;

        [ThreadStatic]
        private static int nestingLevel;

        public override JsonKvlistTagWriterState BeginWriteKvlist()
        {
            MemoryStream stream;
            Utf8JsonWriter writer;

            if (nestingLevel == 0)
            {
                // Top-level kvlist: use ThreadStatic buffer
                if (threadStream == null)
                {
                    threadStream = new MemoryStream();
                    threadWriter = new Utf8JsonWriter(threadStream);
                }
                else
                {
                    threadStream.SetLength(0);
                    threadWriter!.Reset(threadStream);
                }

                stream = threadStream;
                writer = threadWriter!;
            }
            else
            {
                // Nested kvlist: allocate new buffer to avoid corrupting parent
                stream = new MemoryStream();
                writer = new Utf8JsonWriter(stream);
            }

            nestingLevel++;

            writer.WriteStartObject();
            return new(stream, writer, nestingLevel);
        }

        public override void EndWriteKvlist(ref JsonKvlistTagWriterState state)
        {
            state.Writer.WriteEndObject();
            state.Writer.Flush();
            nestingLevel--;
        }

        public override void WriteBooleanValue(ref JsonKvlistTagWriterState state, string key, bool value)
        {
            state.Writer.WriteBoolean(key, value);
        }

        public override void WriteFloatingPointValue(ref JsonKvlistTagWriterState state, string key, double value)
        {
            state.Writer.WriteNumber(key, value);
        }

        public override void WriteIntegralValue(ref JsonKvlistTagWriterState state, string key, long value)
        {
            state.Writer.WriteNumber(key, value);
        }

        public override void WriteNullValue(ref JsonKvlistTagWriterState state, string key)
        {
            state.Writer.WriteNull(key);
        }

        public override void WriteStringValue(ref JsonKvlistTagWriterState state, string key, ReadOnlySpan<char> value)
        {
            state.Writer.WriteString(key, value);
        }

        public override void WriteArrayValue<TArrayState>(ref JsonKvlistTagWriterState state, string key, ref TArrayState arrayState)
        {
            if (arrayState is JsonArrayTagWriterState jsonArrayState)
            {
                var result = jsonArrayState.Stream.TryGetBuffer(out var buffer);
                Debug.Assert(result, "result was false");
                state.Writer.WritePropertyName(key);
#if NET6_0_OR_GREATER
                state.Writer.WriteRawValue(buffer);
#else
                using var doc = JsonDocument.Parse(buffer);
                doc.RootElement.WriteTo(state.Writer);
#endif
            }
        }

        public override void WriteKvlistValue(ref JsonKvlistTagWriterState state, string key, ref JsonKvlistTagWriterState nestedKvlistState)
        {
            var result = nestedKvlistState.Stream.TryGetBuffer(out var buffer);
            Debug.Assert(result, "result was false");
            state.Writer.WritePropertyName(key);
#if NET6_0_OR_GREATER
            state.Writer.WriteRawValue(buffer);
#else
            using var doc = JsonDocument.Parse(buffer);
            doc.RootElement.WriteTo(state.Writer);
#endif
        }
    }
}
