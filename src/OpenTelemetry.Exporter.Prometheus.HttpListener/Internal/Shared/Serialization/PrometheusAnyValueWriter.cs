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
    public static readonly PrometheusAnyValueWriter Instance = new();

    private PrometheusAnyValueWriter()
    {
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

            foreach (var kvp in kvList)
            {
                AnyValue nestedValue = default;
                if (this.TryWriteTag(ref nestedValue, kvp.Key, kvp.Value, tagValueMaxLength))
                {
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
                }
            }

            writer.WriteEndObject();
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

    internal struct AnyValue
    {
        public string? Value;

        public bool IsJsonLiteral;
    }
}
