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

    private ZipkinTagWriter()
    {
    }

    public static ZipkinTagWriter Instance { get; } = new();

    protected override void WriteIntegralTag(ref Utf8JsonWriter writer, string key, long value)
    {
        Span<byte> destination = stackalloc byte[StackallocByteThreshold];
        if (Utf8Formatter.TryFormat(value, destination, out int bytesWritten))
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
        if (Utf8Formatter.TryFormat(value, destination, out int bytesWritten))
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
}
