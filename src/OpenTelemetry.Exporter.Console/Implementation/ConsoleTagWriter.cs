// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var kvp in kvList)
        {
            ConsoleTag nestedTag = default;
            if (this.TryWriteTag(ref nestedTag, kvp.Key, kvp.Value, tagValueMaxLength))
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append('"');
                AppendJsonEscaped(sb, kvp.Key);
                sb.Append("\":");

                var tagValue = nestedTag.Value;
                if (tagValue == null)
                {
                    sb.Append("null");
                }
                else if (IsRawJsonValue(kvp.Value, tagValue))
                {
                    sb.Append(tagValue);
                }
                else
                {
                    sb.Append('"');
                    AppendJsonEscaped(sb, tagValue);
                    sb.Append('"');
                }
            }
        }

        sb.Append('}');
        state.Key = key;
        state.Value = sb.ToString();
    }

    /// <summary>
    /// Determines whether tagValue is already a valid JSON literal
    /// that should be embedded without surrounding quotes.
    /// </summary>
    private static bool IsRawJsonValue(object? originalValue, string tagValue)
    {
        if (originalValue is bool
            or byte or sbyte or short or ushort or int or uint or long
            or float or double)
        {
            return true;
        }

        // KV lists and arrays produce JSON objects/arrays via TryWriteTag.
        // However, when the recursion depth limit is reached, TryWriteTag
        // falls back to a plain string (the type name). Detect this by
        // checking whether the output starts with '{' or '['.
        if ((originalValue is IEnumerable<KeyValuePair<string, object?>> or Array)
            && tagValue.Length > 0
            && (tagValue[0] == '{' || tagValue[0] == '['))
        {
            return true;
        }

        return false;
    }

    private static void AppendJsonEscaped(StringBuilder sb, string value)
    {
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }
    }

    internal struct ConsoleTag
    {
        public string? Key;

        public string? Value;
    }
}
