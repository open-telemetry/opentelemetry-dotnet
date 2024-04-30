// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class ConsoleTagTransformer : TagTransformer<string>
{
    private readonly Action<string, string> onUnsupportedTagDropped;

    public ConsoleTagTransformer(Action<string, string> onUnsupportedTagDropped)
    {
        Debug.Assert(onUnsupportedTagDropped != null, "onUnsupportedTagDropped was null");

        this.onUnsupportedTagDropped = onUnsupportedTagDropped!;
    }

    protected override string TransformIntegralTag(string key, long value) => $"{key}: {value}";

    protected override string TransformFloatingPointTag(string key, double value) => $"{key}: {value}";

    protected override string TransformBooleanTag(string key, bool value) => $"{key}: {(value ? "true" : "false")}";

    protected override string TransformStringTag(string key, string value) => $"{key}: {value}";

    protected override string TransformArrayTag(string key, Array array)
        => this.TransformStringTag(key, TagTransformerJsonHelper.JsonSerializeArrayTag(array));

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        this.onUnsupportedTagDropped(tagKey, tagValueTypeFullName);
    }
}
