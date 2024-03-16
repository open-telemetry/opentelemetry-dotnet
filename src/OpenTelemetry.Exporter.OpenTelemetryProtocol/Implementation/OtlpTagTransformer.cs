// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Internal;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class OtlpTagTransformer : TagTransformer<OtlpCommon.KeyValue>
{
    private OtlpTagTransformer()
    {
    }

    public static OtlpTagTransformer Instance { get; } = new();

    protected override OtlpCommon.KeyValue TransformIntegralTag(string key, long value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = ToAnyValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformFloatingPointTag(string key, double value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = ToAnyValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformBooleanTag(string key, bool value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = ToAnyValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformStringTag(string key, string value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = ToAnyValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformArrayTag(string key, Array array)
    {
        var arrayValue = new OtlpCommon.ArrayValue();

        foreach (var item in array)
        {
            arrayValue.Values.Add(ToAnyValue(item));
        }

        return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { ArrayValue = arrayValue } };
    }

    protected override void OnUnsupportedAttributeDropped(
        string attributeKey,
        string attributeValueTypeFullName)
    {
        OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            attributeValueTypeFullName,
            attributeKey);
    }

    private static OtlpCommon.AnyValue ToAnyValue(long value)
        => new() { IntValue = value };

    private static OtlpCommon.AnyValue ToAnyValue(double value)
       => new() { DoubleValue = value };

    private static OtlpCommon.AnyValue ToAnyValue(bool value)
        => new() { BoolValue = value };

    private static OtlpCommon.AnyValue ToAnyValue(string value)
        => new() { StringValue = value };

    private static OtlpCommon.AnyValue ToAnyValue(object? value)
    {
        if (value == null)
        {
            return new();
        }

        switch (value)
        {
            case char:
            case string:
                // TODO: We don't call TruncateString here so this stringValue
                // won't obey attribute limits

                return ToAnyValue(Convert.ToString(value)!);
            case bool b:
                return ToAnyValue(b);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                return ToAnyValue(Convert.ToInt64(value));
            case float:
            case double:
                return ToAnyValue(Convert.ToDouble(value));
            default:
                // Note: This may throw and we allow that to bubble up so we log
                // the issue.
                var stringValue = Convert.ToString(value);

                if (stringValue == null)
                {
                    return new();
                }

                // TODO: We don't call TruncateString here so this stringValue
                // won't obey attribute limits

                return ToAnyValue(stringValue);
        }
    }
}
