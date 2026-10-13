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

#if NET
    // Each resize formats the whole value again, so start large enough for typical values.
    private const int Utf8BufferInitialSize = 16 * 1024;
    private const int Utf8BufferMaxSize = 1024 * 1024;

    [ThreadStatic]
    private static byte[]? utf8Buffer;
#endif

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

#if NET
    protected override Span<byte> BeginUtf8StringTag(ref Utf8JsonWriter writer, string key)
        => utf8Buffer ??= new byte[Utf8BufferInitialSize];

    protected override bool TryResizeUtf8StringTag(ref Utf8JsonWriter writer, out Span<byte> destination)
    {
        var length = utf8Buffer?.Length ?? 0;
        if (length >= Utf8BufferMaxSize)
        {
            destination = default;
            return false;
        }

        destination = utf8Buffer = new byte[Math.Min(Math.Max(length * 2, Utf8BufferInitialSize), Utf8BufferMaxSize)];
        return true;
    }

    protected override void EndUtf8StringTag(ref Utf8JsonWriter writer, string key, ReadOnlySpan<byte> utf8Value)
        => writer.WriteString(key, utf8Value);
#endif

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

    protected override void WriteKvListTag(ref Utf8JsonWriter writer, string key, IEnumerable<KeyValuePair<string, object?>> value, int? tagValueMaxLength)
    {
        using var stream = new MemoryStream();
        using var kvListWriter = new Utf8JsonWriter(stream);

        // Utf8JsonWriter is a reference type so this alias refers to the
        // same writer; a using local cannot be passed by ref directly.
        var writerAlias = kvListWriter;

        writerAlias.WriteStartObject();

        foreach (var kvp in value)
        {
            this.TryWriteTag(ref writerAlias, kvp.Key, kvp.Value, tagValueMaxLength);
        }

        writerAlias.WriteEndObject();
        writerAlias.Flush();

        writer.WritePropertyName(key);
        writer.WriteStringValue(new ReadOnlySpan<byte>(stream.GetBuffer(), 0, (int)stream.Length));
    }
}
