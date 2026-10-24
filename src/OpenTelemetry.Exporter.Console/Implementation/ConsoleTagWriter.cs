// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class ConsoleTagWriter : JsonStringArrayTagWriter<ConsoleTagWriter.ConsoleTag>
{
    private readonly Action<string, string> onUnsupportedTagDropped;

    public ConsoleTagWriter(Action<string, string> onUnsupportedTagDropped)
    {
        Debug.Assert(onUnsupportedTagDropped != null, "onUnsupportedTagDropped was null");

#if NET
        this.onUnsupportedTagDropped = onUnsupportedTagDropped;
#else
        this.onUnsupportedTagDropped = onUnsupportedTagDropped!;
#endif
    }

    public bool TryTransformTag(KeyValuePair<string, object?> tag, out KeyValuePair<string, string> result)
        => this.TryTransformTag(tag.Key, tag.Value, tagValueMaxLength: null, out result);

    public bool TryTransformTag(KeyValuePair<string, object?> tag, int? tagValueMaxLength, out KeyValuePair<string, string> result)
        => this.TryTransformTag(tag.Key, tag.Value, tagValueMaxLength, out result);

    public bool TryTransformTag(string key, object? value, out KeyValuePair<string, string> result)
        => this.TryTransformTag(key, value, tagValueMaxLength: null, out result);

    public bool TryTransformTag(string key, object? value, int? tagValueMaxLength, out KeyValuePair<string, string> result)
    {
        ConsoleTag consoleTag = default;
        if (this.TryWriteTag(ref consoleTag, key, value, tagValueMaxLength))
        {
            result = new KeyValuePair<string, string>(consoleTag.Key!, consoleTag.Value!);
            return true;
        }

        result = default;
        return false;
    }

    protected override void WriteIntegralTag(ref ConsoleTag state, string key, long value)
    {
        state.Key = key;
        state.Value = value.ToString(CultureInfo.InvariantCulture);
        state.IsJsonLiteral = true;
    }

    protected override void WriteFloatingPointTag(ref ConsoleTag state, string key, double value)
    {
        state.Key = key;
        state.Value = value.ToString(CultureInfo.InvariantCulture);

        // JSON has no representation for NaN or infinity, so those are emitted as strings.
        state.IsJsonLiteral = !double.IsNaN(value) && !double.IsInfinity(value);
    }

    protected override void WriteBooleanTag(ref ConsoleTag state, string key, bool value)
    {
        state.Key = key;
        state.Value = value ? "true" : "false";
        state.IsJsonLiteral = true;
    }

    protected override void WriteStringTag(ref ConsoleTag state, string key, ReadOnlySpan<char> value)
    {
        state.Key = key;
        state.Value = value.ToString();
        state.IsJsonLiteral = false;
    }

    protected override void WriteArrayTag(ref ConsoleTag state, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
        state.Key = key;
#if NET
        state.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
#else
        state.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array, 0, arrayUtf8JsonBytes.Count);
#endif
        state.IsJsonLiteral = true;
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
        => this.onUnsupportedTagDropped(tagKey, tagValueTypeFullName);

    protected override bool TryWriteEmptyTag(ref ConsoleTag state, string key, object? value)
    {
        state.Key = key;
        state.Value = null;
        state.IsJsonLiteral = false;
        return true;
    }

    protected override void WriteKvListTag(ref ConsoleTag state, string key, IEnumerable<KeyValuePair<string, object?>> kvList, int? tagValueMaxLength)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        foreach (var kvp in kvList)
        {
            ConsoleTag nestedTag = default;
            if (this.TryWriteTag(ref nestedTag, kvp.Key, kvp.Value, tagValueMaxLength))
            {
                writer.WritePropertyName(kvp.Key);

                var tagValue = nestedTag.Value;
                if (tagValue == null)
                {
                    writer.WriteNullValue();
                }
                else if (nestedTag.IsJsonLiteral)
                {
                    // The nested write produced JSON (a number, a boolean, an
                    // array or an object), so it is embedded as-is rather than
                    // being quoted as a string.
#if NET
                    writer.WriteRawValue(tagValue);
#else
                    using var doc = JsonDocument.Parse(tagValue);
                    doc.RootElement.WriteTo(writer);
#endif
                }
                else
                {
                    writer.WriteStringValue(tagValue);
                }
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        state.Key = key;
        state.Value = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        state.IsJsonLiteral = true;
    }

    internal struct ConsoleTag
    {
        public string? Key;

        public string? Value;

        // Set when Value is already a JSON literal (a number, a boolean, an
        // array or an object) as opposed to text which has to be quoted when
        // embedded in JSON. Written by the tag writing methods so that nested
        // values do not have to be inferred from the serialized text.
        public bool IsJsonLiteral;
    }
}
