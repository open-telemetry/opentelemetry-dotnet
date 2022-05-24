// <copyright file="TagTransformer.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Internal;

internal abstract class TagTransformer<T>
{
    public T TransformTag(KeyValuePair<string, object> tag)
    {
        if (tag.Value == null)
        {
            return default;
        }

        T result = default;
        switch (tag.Value)
        {
            case char:
            case string:
                return this.TransformStringTag(tag.Key, Convert.ToString(tag.Value));
            case bool b:
                return this.TransformBooleanTag(tag.Key, b);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                return this.TransformIntegralTag(tag.Key, Convert.ToInt64(tag.Value));
            case float:
            case double:
                return this.TransformFloatingPointTag(tag.Key, Convert.ToDouble(tag.Value));
            case Array array:
                return this.TransformArrayTagInternal(tag.Key, array);

            // All other types are converted to strings including the following
            // built-in value types:
            // case nint:    Pointer type.
            // case nuint:   Pointer type.
            // case ulong:   May throw an exception on overflow.
            // case decimal: Converting to double produces rounding errors.
            default:
                try
                {
                    result = this.TransformStringTag(tag.Key, tag.Value.ToString());
                }
                catch
                {
                    // If ToString throws an exception then the tag is ignored.
                }

                break;
        }

        // if (result == null)
        // {
        //     // OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(kvp.Value.GetType().ToString(), kvp.Key);
        // }

        return result;
    }

    protected abstract T TransformIntegralTag(string key, long value);

    protected abstract T TransformFloatingPointTag(string key, double value);

    protected abstract T TransformBooleanTag(string key, bool value);

    protected abstract T TransformStringTag(string key, string value);

    protected virtual T TransformArrayTag(string key, Array array) =>
        this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(array));

    private T TransformArrayTagInternal(string key, Array array)
    {
        // This switch ensures the values of the resultant array-valued tag are of the same type.
        return array switch
        {
            char[] => this.TransformArrayTag(key, array),
            string[] => this.TransformArrayTag(key, array),
            bool[] => this.TransformArrayTag(key, array),
            byte[] => this.TransformArrayTag(key, array),
            sbyte[] => this.TransformArrayTag(key, array),
            short[] => this.TransformArrayTag(key, array),
            ushort[] => this.TransformArrayTag(key, array),
            int[] => this.TransformArrayTag(key, array),
            uint[] => this.TransformArrayTag(key, array),
            long[] => this.TransformArrayTag(key, array),
            float[] => this.TransformArrayTag(key, array),
            double[] => this.TransformArrayTag(key, array),
            _ => this.ConvertToStringArrayThenTransformArrayTag(key, array),
        };
    }

    private T ConvertToStringArrayThenTransformArrayTag(string key, Array array)
    {
        try
        {
            var stringArray = new string[array.Length];

            for (var i = 0; i < array.Length; ++i)
            {
                stringArray[i] = array.GetValue(i)?.ToString();
            }

            return this.TransformArrayTag(key, stringArray);
        }
        catch
        {
            // If an exception is thrown when calling ToString
            // on any element of the array, then the entire array value
            // is ignored.
            return default(T);
        }
    }
}
