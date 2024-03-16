// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal sealed class ZipkinTagTransformer : TagTransformer<string>
{
    private ZipkinTagTransformer()
    {
    }

    public static ZipkinTagTransformer Instance { get; } = new();

    protected override string TransformIntegralTag(string key, long value) => value.ToString();

    protected override string TransformFloatingPointTag(string key, double value) => value.ToString();

    protected override string TransformBooleanTag(string key, bool value) => value ? "true" : "false";

    protected override string TransformStringTag(string key, string value) => value;

    protected override string TransformArrayTag(string key, Array array)
        => this.TransformStringTag(key, TagTransformerJsonHelper.JsonSerializeArrayTag(array));

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        ZipkinExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);
    }
}
