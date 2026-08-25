// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal sealed class ZipkinTagWriter : JsonStringArrayTagWriter<Utf8JsonWriter>
{
    public const int StackallocByteThreshold = 256;

    private const int MaxThreadStaticStreamCapacity = 64 * 1024;

    [ThreadStatic]
    private static MemoryStream?[]? threadStreams;

    [ThreadStatic]
    private static Utf8JsonWriter?[]? threadWriters;

    [ThreadStatic]
    private static int threadNestingLevel;

    private ZipkinTagWriter()
    {
    }

    public static ZipkinTagWriter Instance { get; } = new();

    protected override void WriteIntegralTag(ref Utf8JsonWriter writer, string key, long value)
    {
        Span<byte> destination = stackalloc byte[StackallocByteThreshold];
        if (Utf8Formatter.TryFormat(value, destination, out var bytesWritten))
        {
            writer.WriteString(key, destination.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteString(key, value.ToString(CultureInfo.InvariantCulture));
        }
    }

    protected override void WriteFloatingPointTag(ref Utf8JsonWriter writer, string key, double value)
    {
        Span<byte> destination = stackalloc byte[StackallocByteThreshold];
        if (Utf8Formatter.TryFormat(value, destination, out var bytesWritten))
        {
            writer.WriteString(key, destination.Slice(0, bytesWritten));
        }
        else
        {
            writer.WriteString(key, value.ToString(CultureInfo.InvariantCulture));
        }
    }

    protected override void WriteBooleanTag(ref Utf8JsonWriter writer, string key, bool value)
        => writer.WriteString(key, value ? "true" : "false");

    protected override void WriteStringTag(ref Utf8JsonWriter writer, string key, ReadOnlySpan<char> value)
        => writer.WriteString(key, value);

    protected override void WriteArrayTag(ref Utf8JsonWriter writer, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
        writer.WritePropertyName(key);
        writer.WriteStringValue(arrayUtf8JsonBytes);
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        ZipkinExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);
    }

    protected override bool TryWriteEmptyTag(ref Utf8JsonWriter state, string key, object? value) => false;

    protected override void WriteKvListTag<TEnumerator>(ref Utf8JsonWriter writer, string key, ref TEnumerator kvList, int? tagValueMaxLength)
    {
        // A nested key/value list needs its own writer, so a writer is rented
        // per level of nesting. The nesting is bounded by MaxRecursionDepth.
        var nestingLevel = threadNestingLevel++;

        try
        {
            var (stream, kvListWriter) = RentWriter(nestingLevel);

            kvListWriter.WriteStartObject();

            while (kvList.MoveNext())
            {
                this.TryWriteTag(ref kvListWriter, kvList.CurrentKey, kvList.CurrentValue, tagValueMaxLength);
            }

            kvListWriter.WriteEndObject();
            kvListWriter.Flush();

            writer.WritePropertyName(key);
            writer.WriteStringValue(new ReadOnlySpan<byte>(stream.GetBuffer(), 0, (int)stream.Length));
        }
        finally
        {
            threadNestingLevel--;
        }
    }

    private static (MemoryStream Stream, Utf8JsonWriter Writer) RentWriter(int nestingLevel)
    {
        var streams = threadStreams ??= new MemoryStream?[MaxRecursionDepth];
        var writers = threadWriters ??= new Utf8JsonWriter?[MaxRecursionDepth];

        if ((uint)nestingLevel >= (uint)streams.Length)
        {
            var unpooledStream = new MemoryStream();
            return (unpooledStream, new Utf8JsonWriter(unpooledStream));
        }

        var stream = streams[nestingLevel];
        if (stream == null)
        {
            stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            streams[nestingLevel] = stream;
            writers[nestingLevel] = writer;
            return (stream, writer);
        }

        stream.SetLength(0);
        if (stream.Capacity > MaxThreadStaticStreamCapacity)
        {
            stream.Capacity = 0;
        }

        var pooledWriter = writers[nestingLevel]!;
        pooledWriter.Reset(stream);
        return (stream, pooledWriter);
    }
}
