// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
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

    protected override void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(
            tagValueTypeFullName,
            tagKey);
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

        return value switch
        {
            char => ToAnyValue(Convert.ToString(value)!),
            string s =>
                /* Note: No need to call TruncateString here. That is taken care of
                 in base class via ConvertToStringArrayThenTransformArrayTag */
                ToAnyValue(s),
            bool b => ToAnyValue(b),
            byte b => ToAnyValue(b),
            sbyte b => ToAnyValue(b),
            short s => ToAnyValue(s),
            ushort s => ToAnyValue(s),
            int i => ToAnyValue(i),
            uint i => ToAnyValue(i),
            long l => ToAnyValue(l),
            float f => ToAnyValue(f),
            double d => ToAnyValue(d),
            _ => DefaultCase(value),
        };

        static OtlpCommon.AnyValue DefaultCase(object value)
        {
            // Note: This should never be executed. In the base class the
            // default case in TransformArrayTagInternal converts everything
            // not explicitly supported to strings

            Debug.Fail("Default case executed");

            var stringValue = Convert.ToString(value);

            if (stringValue == null)
            {
                return new();
            }

            return ToAnyValue(stringValue);
        }
    }
}
