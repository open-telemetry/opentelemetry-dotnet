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
using System.Linq;

namespace OpenTelemetry.Internal;

internal abstract class TagTransformer<T>
{
    protected virtual bool JsonifyArrays => false;

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
                return this.JsonifyArrays
                    ? this.ToJsonArray(tag.Key, array)
                    : this.TransformArrayTag(tag.Key, array);

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

    protected virtual T TransformIntegralArrayTag(string key, Array array) => default;

    protected virtual T TransformFloatingPointArrayTag(string key, Array array) => default;

    protected virtual T TransformBooleanArrayTag(string key, Array array) => default;

    protected virtual T TransformStringArrayTag(string key, Array array) => default;

    protected virtual T TransformArrayTag(string key, Array array)
    {
        return array switch
        {
            char[] => this.TransformStringArrayTag(key, array),
            string[] => this.TransformStringArrayTag(key, array),
            bool[] => this.TransformBooleanArrayTag(key, array),
            byte[] => this.TransformIntegralArrayTag(key, array),
            sbyte[] => this.TransformIntegralArrayTag(key, array),
            short[] => this.TransformIntegralArrayTag(key, array),
            ushort[] => this.TransformIntegralArrayTag(key, array),
            int[] => array is nint[] ? this.TransformStringArrayTag(key, array) : this.TransformIntegralArrayTag(key, array),
            uint[] => this.TransformIntegralArrayTag(key, array),
            long[] => array is nuint[] ? this.TransformStringArrayTag(key, array) : this.TransformIntegralArrayTag(key, array),
            float[] => this.TransformFloatingPointArrayTag(key, array),
            double[] => this.TransformFloatingPointArrayTag(key, array),
            _ => this.TransformStringArrayTag(key, array),
        };
    }

    private T ToJsonArray(string key, Array array)
    {
        return array switch
        {
            char[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            string[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            bool[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            byte[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            sbyte[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            short[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            ushort[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            int[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            uint[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            long[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            float[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            double[] arr => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(arr)),
            _ => this.StringifyArray(key, array),
        };
    }

    private T StringifyArray(string key, Array array)
    {
        try
        {
            return this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize((array as object[]).Select(x => x?.ToString())));
        }
        catch
        {
            return default;
        }
    }
}
