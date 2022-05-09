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
using Google.Protobuf.Collections;
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

            var attrib = new OtlpCommon.KeyValue { Key = kvp.Key, Value = new OtlpCommon.AnyValue { } };

            switch (kvp.Value)
            {
                case char:
                case string:
                    attrib.Value.StringValue = Convert.ToString(kvp.Value);
                    break;
                case bool b:
                    attrib.Value.BoolValue = b;
                    break;
                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                    attrib.Value.IntValue = Convert.ToInt64(kvp.Value);
                    break;
                case float:
                case double:
                    attrib.Value.DoubleValue = Convert.ToDouble(kvp.Value);
                    break;
                case Array array:
                    var arrayValue = attrib.Value.ArrayValue = new OtlpCommon.ArrayValue();
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
                    switch (kvp.Value)
                    {
                        case char[]:
                        case string[]:
                            foreach (var item in array)
                            {
                                arrayValue.Values.Add(new OtlpCommon.AnyValue { StringValue = Convert.ToString(item) });
                            }

                            break;
                        case bool[]:
                            foreach (var item in array)
                            {
                                arrayValue.Values.Add(new OtlpCommon.AnyValue { BoolValue = Convert.ToBoolean(item) });
                            }

                            break;
                        case byte[]:
                        case sbyte[]:
                        case short[]:
                        case ushort[]:
                        case int[]:
                        case uint[]:
                        case long[]:
                            foreach (var item in array)
                            {
                                arrayValue.Values.Add(new OtlpCommon.AnyValue { IntValue = Convert.ToInt64(item) });
                            }

                            break;
                        case float[]:
                        case double[]:
                            foreach (var item in array)
                            {
                                arrayValue.Values.Add(new OtlpCommon.AnyValue { DoubleValue = Convert.ToDouble(item) });
                            }

                            break;
                        default:
                            return null;
                    }
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly

                    break;

                // case nint:    Pointer type.
                // case nuint:   Pointer type.
                // case ulong:   May throw an exception on overflow.
                // case decimal: Converting to double produces rounding errors.
                default:
                    return null;
            }

            return attrib;
        }
    }
}
