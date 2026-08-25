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
        => this.TryTransformTag(tag.Key, tag.Value, out result);

    public bool TryTransformTag(string key, object? value, out KeyValuePair<string, string> result)
    {
        ConsoleTag consoleTag = default;
        if (this.TryWriteTag(ref consoleTag, key, value))
        {
            result = new KeyValuePair<string, string>(consoleTag.Key!, consoleTag.Value!);
            return true;
        }

        result = default;
        return false;
    }

    protected override void WriteIntegralTag(ref ConsoleTag consoleTag, string key, long value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString(CultureInfo.InvariantCulture);
        consoleTag.IsJsonLiteral = true;
    }

    protected override void WriteFloatingPointTag(ref ConsoleTag consoleTag, string key, double value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString(CultureInfo.InvariantCulture);

        // JSON has no representation for NaN or infinity, so those are emitted as strings.
        consoleTag.IsJsonLiteral = !double.IsNaN(value) && !double.IsInfinity(value);
    }

    protected override void WriteBooleanTag(ref ConsoleTag consoleTag, string key, bool value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value ? "true" : "false";
        consoleTag.IsJsonLiteral = true;
    }

    protected override void WriteStringTag(ref ConsoleTag consoleTag, string key, ReadOnlySpan<char> value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString();
        consoleTag.IsJsonLiteral = false;
    }

    protected override void WriteArrayTag(ref ConsoleTag consoleTag, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
        consoleTag.Key = key;
#if NET
        consoleTag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
#else
        consoleTag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array, 0, arrayUtf8JsonBytes.Count);
#endif
        consoleTag.IsJsonLiteral = true;
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
        => this.onUnsupportedTagDropped(tagKey, tagValueTypeFullName);

    protected override bool TryWriteEmptyTag(ref ConsoleTag consoleTag, string key, object? value)
    {
        consoleTag.Key = key;
        consoleTag.Value = null;
        consoleTag.IsJsonLiteral = false;
        return true;
    }

    protected override void WriteKvListTag<TEnumerator>(ref ConsoleTag state, string key, ref TEnumerator kvList, int? tagValueMaxLength)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        while (kvList.MoveNext())
        {
            var kvpKey = kvList.CurrentKey;
            ConsoleTag nestedTag = default;
            if (this.TryWriteTag(ref nestedTag, kvpKey, kvList.CurrentValue, tagValueMaxLength))
            {
                writer.WritePropertyName(kvpKey);

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
