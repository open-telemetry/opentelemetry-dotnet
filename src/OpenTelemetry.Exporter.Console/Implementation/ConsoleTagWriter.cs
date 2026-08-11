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
    }

    protected override void WriteFloatingPointTag(ref ConsoleTag consoleTag, string key, double value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString(CultureInfo.InvariantCulture);
    }

    protected override void WriteBooleanTag(ref ConsoleTag consoleTag, string key, bool value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value ? "true" : "false";
    }

    protected override void WriteStringTag(ref ConsoleTag consoleTag, string key, ReadOnlySpan<char> value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString();
    }

    protected override void WriteArrayTag(ref ConsoleTag consoleTag, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
        consoleTag.Key = key;
#if NET
        consoleTag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
#else
        consoleTag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array, 0, arrayUtf8JsonBytes.Count);
#endif
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
        => this.onUnsupportedTagDropped(tagKey, tagValueTypeFullName);

    protected override bool TryWriteEmptyTag(ref ConsoleTag consoleTag, string key, object? value)
    {
        consoleTag.Key = key;
        consoleTag.Value = null;
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
                else if (IsRawJsonValue(kvp.Value, tagValue))
                {
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
    }

    /// <summary>
    /// Determines whether tagValue is already a valid JSON literal
    /// that should be embedded without surrounding quotes.
    /// </summary>
    private static bool IsRawJsonValue(object? originalValue, string tagValue)
    {
        if (originalValue is float or double)
        {
            return tagValue is not ("NaN" or "Infinity" or "-Infinity");
        }

        if (originalValue is bool or byte or sbyte or short or ushort or int or uint or long)
        {
            return true;
        }

        // KV lists and arrays produce JSON objects/arrays via TryWriteTag.
        // However, when the recursion depth limit is reached, TryWriteTag
        // falls back to a plain string (the type name). Detect this by
        // checking whether the output starts with '{' or '['.
        if ((originalValue is IEnumerable<KeyValuePair<string, object?>> or Array)
            && tagValue.Length > 0
            && (tagValue[0] is '{' or '['))
        {
            return true;
        }

        return false;
    }

    internal struct ConsoleTag
    {
        public string? Key;

        public string? Value;
    }
}
