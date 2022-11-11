// <copyright file="OtlpKeyValueTransformer.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using OpenTelemetry.Internal;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class OtlpKeyValueTransformer : TagAndValueTransformer<OtlpCommon.KeyValue, OtlpCommon.AnyValue>
{
    private OtlpKeyValueTransformer()
    {
    }

    public static OtlpKeyValueTransformer Instance { get; } = new();

    protected override OtlpCommon.KeyValue TransformIntegralTag(string key, long value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = this.TransformIntegralValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformFloatingPointTag(string key, double value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = this.TransformFloatingPointValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformBooleanTag(string key, bool value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = this.TransformBooleanValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformStringTag(string key, string value)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = this.TransformStringValue(value) };
    }

    protected override OtlpCommon.KeyValue TransformArrayTag(string key, Array array)
    {
        return new OtlpCommon.KeyValue { Key = key, Value = this.TransformArrayValue(array) };
    }

    protected override OtlpCommon.AnyValue TransformIntegralValue(long value)
    {
        return new OtlpCommon.AnyValue { IntValue = value };
    }

    protected override OtlpCommon.AnyValue TransformFloatingPointValue(double value)
    {
        return new OtlpCommon.AnyValue { DoubleValue = value };
    }

    protected override OtlpCommon.AnyValue TransformBooleanValue(bool value)
    {
        return new OtlpCommon.AnyValue { BoolValue = value };
    }

    protected override OtlpCommon.AnyValue TransformStringValue(string value)
    {
        return new OtlpCommon.AnyValue { StringValue = value };
    }

    protected override OtlpCommon.AnyValue TransformArrayValue(Array array)
    {
        var arrayValue = new OtlpCommon.ArrayValue();

        foreach (var item in array)
        {
            try
            {
                var value = item != null ? this.TransformValue(item) : new OtlpCommon.AnyValue();
                arrayValue.Values.Add(value);
            }
            catch
            {
                return null;
            }
        }

        return new OtlpCommon.AnyValue { ArrayValue = arrayValue };
    }
}
