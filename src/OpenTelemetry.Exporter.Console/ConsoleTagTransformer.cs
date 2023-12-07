// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class ConsoleTagTransformer : TagTransformer<string>
{
    private ConsoleTagTransformer()
    {
    }

    public static ConsoleTagTransformer Instance { get; } = new();

    protected override string TransformIntegralTag(string key, long value) => $"{key}: {value}";

    protected override string TransformFloatingPointTag(string key, double value) => $"{key}: {value}";

    protected override string TransformBooleanTag(string key, bool value) => $"{key}: {(value ? "true" : "false")}";

    protected override string TransformStringTag(string key, string value) => $"{key}: {value}";

    protected override string TransformArrayTag(string key, Array array)
        => this.TransformStringTag(key, TagTransformerJsonHelper.JsonSerializeArrayTag(array));
}
