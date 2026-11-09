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
        // Like an array, an oversized map is replaced with "TRUNCATED". The entries are bounded first so
        // that the keys of an unbounded map are not buffered in order to deduplicate them.
        if (!TryDeduplicateKeys(kvList, GetMaxEntries(this.maxJsonLength), out var entries))
        {
            this.WriteTruncated(ref state, key, kvList);
            return;
        }

        using var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);

        try
        {
            writer.WriteStartObject();

            foreach (var kvp in entries)
            {
                // A name or value that cannot fit is rejected before it is converted (or its escaped form,
                // which is up to six times larger, is buffered) by working out how big that would be.
                var pendingLength = this.GetPendingLength(writer, kvp, tagValueMaxLength, out var includesValue);
                if (this.IsTooLong(writer, pendingLength))
                {
                    this.WriteTruncated(ref state, key, kvList);
                    return;
                }

                AnyValue nestedValue = default;
                if (this.TryWriteTag(ref nestedValue, kvp.Key, kvp.Value, tagValueMaxLength))
                {
                    long valueLength = nestedValue.Value?.Length ?? 0;
#if !NET
                    if (nestedValue.IsJsonLiteral && nestedValue.Value != null)
                    {
                        var remaining = this.maxJsonLength - writer.BytesCommitted - writer.BytesPending - pendingLength;
                        valueLength = GetEmbeddedJsonLength(nestedValue.Value, remaining);
                    }
#endif
                    if (this.IsTooLong(writer, includesValue ? pendingLength : pendingLength + valueLength))
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

#if !NET
    private static long GetEmbeddedJsonLength(ReadOnlySpan<char> value, long limit)
    {
        long length = value.Length;
        var inString = false;

        // JsonElement.WriteTo re-encodes strings on the legacy targets. The array
        // writer emits raw '+' in Base64 strings, so allow for its JSON escaping.
        // Plus signs in numeric exponents and already escaped strings stay unchanged.
        for (var i = 0; i < value.Length && length <= limit; i++)
        {
            switch (value[i])
            {
                case '\\' when inString:
                    i++;
                    break;

                case '"':
                    inString = !inString;
                    break;

                case '+' when inString:
                    length += 5;
                    break;
            }
        }

        return length;
    }
#endif

    private static long GetEscapedBase64StringLength(ReadOnlySpan<byte> value, long limit)
    {
        var length = GetBase64StringLength(value.Length);

        // Base64 index 62 is '+', which the default JSON encoder writes as \u002B.
        // Count those indices directly so the five extra bytes per '+' are included
        // without allocating the encoded string. Padding cannot introduce a '+'.
        for (var i = 0; i < value.Length && length <= limit; i += 3)
        {
            var first = value[i];
            if (first >> 2 == 62)
            {
                length += 5;
            }

            if (i + 1 >= value.Length)
            {
                break;
            }

            var second = value[i + 1];
            if ((((first & 0x03) << 4) | (second >> 4)) == 62)
            {
                length += 5;
            }

            if (i + 2 >= value.Length)
            {
                break;
            }

            var third = value[i + 2];
            if ((((second & 0x0F) << 2) | (third >> 6)) == 62)
            {
                length += 5;
            }

            if ((third & 0x3F) == 62)
            {
                length += 5;
            }
        }

        return length;
    }

    private long GetPendingLength(Utf8JsonWriter writer, KeyValuePair<string, object?> entry, int? tagValueMaxLength, out bool includesValue)
    {
        includesValue = false;
        var remaining = this.maxJsonLength - writer.BytesCommitted - writer.BytesPending;
        var length = GetEscapedLength(entry.Key, remaining) + 2;

        switch (entry.Value)
        {
            case string stringValue:
                var characters = stringValue.AsSpan();
                if (tagValueMaxLength is { } maxChars && characters.Length > maxChars)
                {
                    characters = characters.Slice(0, maxChars);
                }

                length += GetEscapedLength(characters, remaining - length) + 2;
                includesValue = true;
                break;

            case byte[] bytes when bytes.GetType() == typeof(byte[]):
                var byteCount = tagValueMaxLength is { } maxBytes ? Math.Min(bytes.Length, maxBytes) : bytes.Length;
                length += GetEscapedBase64StringLength(bytes.AsSpan(0, byteCount), remaining - length);
                includesValue = true;
                break;
        }

        return length;
    }

    private bool IsTooLong(Utf8JsonWriter writer, long pendingLength)
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
