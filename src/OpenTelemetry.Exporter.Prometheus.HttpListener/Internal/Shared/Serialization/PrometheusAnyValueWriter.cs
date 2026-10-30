// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

// Computes the string representation of an array- or map-valued attribute for use as a
// Prometheus label value. As described by
// https://github.com/open-telemetry/opentelemetry-specification/blob/v1.60.0/specification/common/README.md#anyvalue-representation-for-non-otlp-protocols.
// This JSON-encodes the value (numbers/booleans/nested arrays/maps keep their native JSON
// typing, byte arrays are Base64-encoded). Scalar values never reach this writer;
// TextFormatSerializer.GetLabelValueString formats those directly.
internal sealed class PrometheusAnyValueWriter : JsonStringArrayTagWriter<PrometheusAnyValueWriter.AnyValue>
{
    // A label value is ultimately copied into the scrape response, so its JSON representation is
    // capped at the same size as the serialized tags of a histogram. See TextFormatSerializer.
    public static readonly PrometheusAnyValueWriter Instance = new(TextFormatSerializer.MaxSerializedTagsBufferSize);

    private readonly long maxJsonLength;

    internal PrometheusAnyValueWriter(long maxJsonLength)
        : base(maxJsonLength)
    {
        this.maxJsonLength = maxJsonLength;
    }

    public string ToLabelValueString(string key, object? value)
    {
        AnyValue state = default;
        return this.TryWriteTag(ref state, key, value) ? state.Value ?? string.Empty : string.Empty;
    }

    protected override void WriteIntegralTag(ref AnyValue state, string key, long value)
    {
        state.Value = value.ToString(CultureInfo.InvariantCulture);
        state.IsJsonLiteral = true;
    }

    protected override void WriteFloatingPointTag(ref AnyValue state, string key, double value)
    {
        state.Value = value.ToString(CultureInfo.InvariantCulture);

        // JSON has no representation for NaN or infinity, so those are emitted as strings.
        state.IsJsonLiteral = !double.IsNaN(value) && !double.IsInfinity(value);
    }

    protected override void WriteBooleanTag(ref AnyValue state, string key, bool value)
    {
        state.Value = value ? "true" : "false";
        state.IsJsonLiteral = true;
    }

    protected override void WriteStringTag(ref AnyValue state, string key, ReadOnlySpan<char> value)
    {
        state.Value = value.ToString();
        state.IsJsonLiteral = false;
    }

    protected override void WriteArrayTag(ref AnyValue state, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
#if NET
        state.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, arrayUtf8JsonBytes.Offset, arrayUtf8JsonBytes.Count);
#else
        state.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array, arrayUtf8JsonBytes.Offset, arrayUtf8JsonBytes.Count);
#endif
        state.IsJsonLiteral = true;
    }

    protected override void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName)
        => PrometheusExporterEventSource.Log.UnsupportedAttributeType(tagValueTypeFullName, tagKey);

    protected override bool TryWriteEmptyTag(ref AnyValue state, string key, object? value)
    {
        // Empty values are represented as JSON null when nested (as an array element or map
        // value); this writer is only reached for the array/map case, so this is always the
        // nested rule, never the top-level "empty string" rule.
        state.Value = null;
        state.IsJsonLiteral = true;
        return true;
    }

    protected override void WriteKvListTag(
        ref AnyValue state,
        string key,
        IEnumerable<KeyValuePair<string, object?>> kvList,
        int? tagValueMaxLength)
    {
        using var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);

        try
        {
            writer.WriteStartObject();

            foreach (var kvp in DeduplicateKeys(kvList))
            {
                AnyValue nestedValue = default;
                if (this.TryWriteTag(ref nestedValue, kvp.Key, kvp.Value, tagValueMaxLength))
                {
                    // Every character is encoded as at least one byte, so a name or value that
                    // cannot fit is rejected before the (up to six times larger) escaped form of
                    // it is buffered. Like an array, an oversized map is replaced with "TRUNCATED".
                    if (this.IsTooLong(writer, kvp.Key.Length + (nestedValue.Value?.Length ?? 0)))
                    {
                        this.WriteTruncated(ref state, key, kvList);
                        return;
                    }

                    writer.WritePropertyName(kvp.Key);

                    if (nestedValue.Value == null)
                    {
                        writer.WriteNullValue();
                    }
                    else if (nestedValue.IsJsonLiteral)
                    {
                        // The nested write produced JSON (a number, a boolean, an array or an
                        // object), so it is embedded as-is rather than being quoted as a string.
#if NET
                        writer.WriteRawValue(nestedValue.Value);
#else
                        using var doc = JsonDocument.Parse(nestedValue.Value);
                        doc.RootElement.WriteTo(writer);
#endif
                    }
                    else
                    {
                        writer.WriteStringValue(nestedValue.Value);
                    }

                    if (this.IsTooLong(writer, 0))
                    {
                        this.WriteTruncated(ref state, key, kvList);
                        return;
                    }
                }
            }

            writer.WriteEndObject();

            if (this.IsTooLong(writer, 0))
            {
                this.WriteTruncated(ref state, key, kvList);
                return;
            }

            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        var success = stream.TryGetBuffer(out var buffer);
        Debug.Assert(success, "success was false");

#if NET
        state.Value = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
#else
        state.Value = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
#endif
        state.IsJsonLiteral = true;
    }

    private bool IsTooLong(Utf8JsonWriter writer, int pendingLength)
        => writer.BytesCommitted + writer.BytesPending + pendingLength > this.maxJsonLength;

    private void WriteTruncated(ref AnyValue state, string key, IEnumerable<KeyValuePair<string, object?>> kvList)
    {
        state.Value = "TRUNCATED";
        state.IsJsonLiteral = false;
        this.OnUnsupportedTagDropped(key, kvList.GetType().ToString());
    }

    internal struct AnyValue
    {
        public string? Value;

        public bool IsJsonLiteral;
    }
}
