// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class ConsoleTagWriter : JsonStringArrayTagWriter<ConsoleTagWriter.ConsoleTag>
{
    private readonly Action<string, string> onUnsupportedTagDropped;

    public ConsoleTagWriter(Action<string, string> onUnsupportedTagDropped)
    {
        this.onUnsupportedTagDropped = onUnsupportedTagDropped;
    }

    public bool TryTransformTag(KeyValuePair<string, object?> tag, out KeyValuePair<string, string> result)
    {
        return this.TryTransformTag(tag.Key, tag.Value, out result);
    }

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
        consoleTag.Value = value.ToString();
    }

    protected override void WriteFloatingPointTag(ref ConsoleTag consoleTag, string key, double value)
    {
        consoleTag.Key = key;
        consoleTag.Value = value.ToString();
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
        consoleTag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
    }

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        this.onUnsupportedTagDropped(tagKey, tagValueTypeFullName);
    }

    internal struct ConsoleTag
    {
        public string? Key;

        public string? Value;
    }
}
