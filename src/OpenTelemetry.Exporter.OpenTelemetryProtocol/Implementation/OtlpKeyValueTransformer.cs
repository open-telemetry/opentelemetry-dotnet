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
using OtlpCommon = Opentelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal class OtlpKeyValueTransformer : TagTransformer<OtlpCommon.KeyValue>
    {
        protected override OtlpCommon.KeyValue TransformIntegralTag(string key, long value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { IntValue = value } };
        }

        protected override OtlpCommon.KeyValue TransformFloatingPointTag(string key, double value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { DoubleValue = value } };
        }

        protected override OtlpCommon.KeyValue TransformBooleanTag(string key, bool value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { BoolValue = value } };
        }

        protected override OtlpCommon.KeyValue TransformStringTag(string key, string value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { StringValue = value } };
        }

        protected override OtlpCommon.KeyValue TransformIntegralArrayTag(string key, Array array)
        {
            var arrayValue = new OtlpCommon.ArrayValue();

            foreach (var item in array)
            {
                var anyValue = new OtlpCommon.AnyValue { IntValue = Convert.ToInt64(item) };
                arrayValue.Values.Add(anyValue);
            }

            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { ArrayValue = arrayValue } };
        }

        protected override OtlpCommon.KeyValue TransformFloatingPointArrayTag(string key, Array array)
        {
            var arrayValue = new OtlpCommon.ArrayValue();

            foreach (var item in array)
            {
                var anyValue = new OtlpCommon.AnyValue { DoubleValue = Convert.ToDouble(item) };
                arrayValue.Values.Add(anyValue);
            }

            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { ArrayValue = arrayValue } };
        }

        protected override OtlpCommon.KeyValue TransformBooleanArrayTag(string key, Array array)
        {
            var arrayValue = new OtlpCommon.ArrayValue();

            foreach (var item in array)
            {
                var anyValue = new OtlpCommon.AnyValue { BoolValue = Convert.ToBoolean(item) };
                arrayValue.Values.Add(anyValue);
            }

            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { ArrayValue = arrayValue } };
        }

        protected override OtlpCommon.KeyValue TransformStringArrayTag(string key, Array array)
        {
            var arrayValue = new OtlpCommon.ArrayValue();

            foreach (var item in array)
            {
                try
                {
                    var value = item != null
                        ? Convert.ToString(item)
                        : null;

                    var anyValue = item != null ? new OtlpCommon.AnyValue { StringValue = value } : new OtlpCommon.AnyValue { };
                    arrayValue.Values.Add(anyValue);
                }
                catch
                {
                    return default;
                }
            }

            return new OtlpCommon.KeyValue { Key = key, Value = new OtlpCommon.AnyValue { ArrayValue = arrayValue } };
        }
    }
}
