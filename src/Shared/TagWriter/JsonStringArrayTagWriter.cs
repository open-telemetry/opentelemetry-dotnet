// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenTelemetry.Internal;

internal abstract class JsonStringArrayTagWriter<TTagState> : TagWriter<TTagState, JsonStringArrayTagWriter<TTagState>.JsonArrayTagWriterState>
    where TTagState : notnull
{
    protected JsonStringArrayTagWriter()
        : this(long.MaxValue)
    {
    }

    // Arrays whose JSON representation would exceed maxJsonLength bytes are not serialized;
    // the writing is abandoned part-way through, before the representation is fully allocated,
    // and the array is replaced with "TRUNCATED" (as done for the other array writers).
    protected JsonStringArrayTagWriter(long maxJsonLength)
        : base(new JsonArrayTagWriter(maxJsonLength))
    {
    }

    internal static long GetEscapedLength(ReadOnlySpan<char> value, long limit)
    {
        long length = 0;

        foreach (var c in value)
        {
            length += c switch
            {
                '\b' or '\t' or '\n' or '\f' or '\r' or '\\' => 2,
                < ' ' => 6,
                <= '~' => JavaScriptEncoder.Default.WillEncode(c) ? 6 : 1,
                _ => 6,
            };

            if (length > limit)
            {
                break;
            }
        }

        return length;
    }

    internal static long GetBase64StringLength(int byteCount)
        => (((long)byteCount + 2) / 3 * 4) + 2;

    internal static long GetMaxEntries(long maxJsonLength)
        => Math.Max(0, (maxJsonLength - 1) / 5);

    protected sealed override void WriteArrayTag(ref TTagState state, string key, ref JsonArrayTagWriterState value)
    {
        var result = value.Stream.TryGetBuffer(out var buffer);

        Debug.Assert(result, "result was false");

        this.WriteArrayTag(ref state, key, buffer);
    }

    protected abstract void WriteArrayTag(ref TTagState state, string key, ArraySegment<byte> arrayUtf8JsonBytes);

    protected override bool TryWriteByteArrayTag(ref TTagState state, string key, ReadOnlySpan<byte> value)
    {
        // See https://github.com/open-telemetry/opentelemetry-specification/blob/v1.60.0/specification/common/README.md#byte-arrays;
        // byte arrays SHOULD be Base64-encoded when represented as a string for non-OTLP protocols.
#if NET
        var base64 = Convert.ToBase64String(value);
#else
        var base64 = Convert.ToBase64String(value.ToArray());
#endif
        this.WriteStringTag(ref state, key, base64.AsSpan());
        return true;
    }

    internal readonly struct JsonArrayTagWriterState(MemoryStream stream, Utf8JsonWriter writer)
    {
        public MemoryStream Stream { get; } = stream;

        public Utf8JsonWriter Writer { get; } = writer;
    }

    internal sealed class JsonArrayTagWriter : ArrayTagWriter<JsonArrayTagWriterState>
    {
        private const int MaxThreadStaticStreamCapacity = 64 * 1024;

        [ThreadStatic]
        private static MemoryStream? threadStream;

        [ThreadStatic]
        private static Utf8JsonWriter? threadWriter;

        private readonly long maxJsonLength;

        public JsonArrayTagWriter(long maxJsonLength)
        {
            this.maxJsonLength = maxJsonLength;
        }

        public override long MaxNestedObjectEntries => GetMaxEntries(this.maxJsonLength);

        public override JsonArrayTagWriterState BeginWriteArray()
        {
            var state = EnsureWriter();
            state.Writer.WriteStartArray();
            return state;
        }

        public override void EndWriteArray(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteEndArray();
            this.ThrowIfTooLong(state, 0);
            state.Writer.Flush();
        }

        public override void WriteBooleanValue(ref JsonArrayTagWriterState state, bool value)
        {
            state.Writer.WriteBooleanValue(value);
            this.ThrowIfTooLong(state, 0);
        }

        public override void WriteFloatingPointValue(ref JsonArrayTagWriterState state, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                // JSON has no representation for NaN or infinity, so those are emitted as strings.
                state.Writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                state.Writer.WriteNumberValue(value);
            }

            this.ThrowIfTooLong(state, 0);
        }

        public override void WriteIntegralValue(ref JsonArrayTagWriterState state, long value)
        {
            state.Writer.WriteNumberValue(value);
            this.ThrowIfTooLong(state, 0);
        }

        public override void WriteNullValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteNullValue();
            this.ThrowIfTooLong(state, 0);
        }

        public override void WriteStringValue(ref JsonArrayTagWriterState state, ReadOnlySpan<char> value)
        {
            // This rejects a value that cannot fit before the (up to six times larger) escaped form of it is buffered.
            this.ThrowIfStringTooLong(state, value);
            state.Writer.WriteStringValue(value);
            this.ThrowIfTooLong(state, 0);
        }

        public override bool TryWriteByteArrayValue(ref JsonArrayTagWriterState state, ReadOnlySpan<byte> value)
        {
            // See https://github.com/open-telemetry/opentelemetry-specification/blob/v1.60.0/specification/common/README.md#byte-arrays;
            // byte arrays are Base64-encoded when nested inside another array or map, the same
            // as when they are the top-level attribute value.
            this.ThrowIfTooLong(state, GetBase64StringLength(value.Length));
            state.Writer.WriteBase64StringValue(value);
            this.ThrowIfTooLong(state, 0);
            return true;
        }

        public override bool TryBeginNestedArrayValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteStartArray();
            this.ThrowIfTooLong(state, 0);
            return true;
        }

        public override void EndNestedArrayValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteEndArray();
            this.ThrowIfTooLong(state, 0);
        }

        public override bool TryBeginNestedObjectValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteStartObject();
            this.ThrowIfTooLong(state, 0);
            return true;
        }

        public override void WriteNestedObjectPropertyName(ref JsonArrayTagWriterState state, string name)
        {
            // As for a string value, reject a name that cannot fit before its escaped form is buffered.
            this.ThrowIfStringTooLong(state, name);
            state.Writer.WritePropertyName(name);
            this.ThrowIfTooLong(state, 0);
        }

        public override void EndNestedObjectValue(ref JsonArrayTagWriterState state)
        {
            state.Writer.WriteEndObject();
            this.ThrowIfTooLong(state, 0);
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
                if (threadStream.Capacity > MaxThreadStaticStreamCapacity)
                {
                    threadStream.Capacity = 0;
                }

                threadWriter!.Reset(threadStream);
                return new(threadStream, threadWriter);
            }
        }

        // Signals "the buffer is too small" in the way TagWriter expects of an array writer. As
        // TryResize is not overridden the buffer cannot be grown, so the array is dropped.
        private void ThrowIfStringTooLong(JsonArrayTagWriterState state, ReadOnlySpan<char> value)
        {
            var remaining = this.maxJsonLength - state.Writer.BytesCommitted - state.Writer.BytesPending;
            this.ThrowIfTooLong(state, GetEscapedLength(value, remaining) + 2);
        }

        private void ThrowIfTooLong(JsonArrayTagWriterState state, long pendingLength)
        {
            if (state.Writer.BytesCommitted + state.Writer.BytesPending + pendingLength > this.maxJsonLength)
            {
                throw new ArgumentException("The JSON representation of the array exceeds the maximum supported size.");
            }
        }
    }
}
