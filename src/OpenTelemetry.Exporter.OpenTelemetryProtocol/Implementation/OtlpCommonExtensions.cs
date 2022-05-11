// <copyright file="OtlpCommonExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OtlpCommon = Opentelemetry.Proto.Common.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class OtlpCommonExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OtlpCommon.KeyValue ToOtlpAttribute(this KeyValuePair<string, object> kvp)
        {
            if (kvp.Value == null)
            {
                return null;
            }

            var value = ToOtlpValue(kvp.Value);

            if (value == null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(kvp.Value.GetType().ToString(), kvp.Key);
                return null;
            }

            return new OtlpCommon.KeyValue { Key = kvp.Key, Value = value };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpCommon.AnyValue ToOtlpValue(object value)
        {
            switch (value)
            {
                case char:
                case string:
                    return new OtlpCommon.AnyValue { StringValue = Convert.ToString(value) };
                case bool b:
                    return new OtlpCommon.AnyValue { BoolValue = b };
                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                    return new OtlpCommon.AnyValue { IntValue = Convert.ToInt64(value) };
                case float:
                case double:
                    return new OtlpCommon.AnyValue { DoubleValue = Convert.ToDouble(value) };
                case Array array:
                    return ToOtlpArrayValue(array);

                // All other types are converted to strings including the following
                // built-in value types:
                // case nint:    Pointer type.
                // case nuint:   Pointer type.
                // case ulong:   May throw an exception on overflow.
                // case decimal: Converting to double produces rounding errors.
                default:
                    try
                    {
                        return new OtlpCommon.AnyValue { StringValue = value.ToString() };
                    }
                    catch
                    {
                        return null;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpCommon.AnyValue ToOtlpArrayValue(Array array)
        {
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
            var arrayValue = new OtlpCommon.ArrayValue();
            switch (array)
            {
                case char[]:
                case string[]:
                case bool[]:
                case byte[]:
                case sbyte[]:
                case short[]:
                case ushort[]:
                case int[]:
                case uint[]:
                case long[]:
                case float[]:
                case double[]:
                    foreach (var item in array)
                    {
                        arrayValue.Values.Add(ToOtlpValue(item));
                    }

                    return new OtlpCommon.AnyValue { ArrayValue = arrayValue };
                default:
                    foreach (var item in array)
                    {
                        try
                        {
                            arrayValue.Values.Add(ToOtlpValue(item.ToString()));
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    return new OtlpCommon.AnyValue { ArrayValue = arrayValue };
            }
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        }
    }
}
