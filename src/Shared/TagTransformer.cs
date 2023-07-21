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

namespace OpenTelemetry.Internal;

internal abstract class TagTransformer<T>
{
    public static Action<string, string> LogUnsupportedAttributeType = null;

    public bool TryTransformTag(KeyValuePair<string, object> tag, out T result, int? maxLength = null)
    {
        if (tag.Value == null)
        {
            result = default;
            return false;
        }

        switch (tag.Value)
        {
            case char:
            case string:
                result = this.TransformStringTag(tag.Key, TruncateString(Convert.ToString(tag.Value), maxLength));
                break;
            case bool b:
                result = this.TransformBooleanTag(tag.Key, b);
                break;
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                result = this.TransformIntegralTag(tag.Key, Convert.ToInt64(tag.Value));
                break;
            case float:
            case double:
                result = this.TransformFloatingPointTag(tag.Key, Convert.ToDouble(tag.Value));
                break;
            case Array array:
                try
                {
                    result = this.TransformArrayTagInternal(tag.Key, array, maxLength);
                }
                catch
                {
                    // If an exception is thrown when calling ToString
                    // on any element of the array, then the entire array value
                    // is ignored.
                    LogUnsupportedAttributeType?.Invoke(tag.Value.GetType().ToString(), tag.Key);
                    result = default;
                    return false;
                }

                break;

            // All other types are converted to strings including the following
            // built-in value types:
            // case nint:    Pointer type.
            // case nuint:   Pointer type.
            // case ulong:   May throw an exception on overflow.
            // case decimal: Converting to double produces rounding errors.
            default:
                try
                {
                    result = this.TransformStringTag(tag.Key, TruncateString(tag.Value.ToString(), maxLength));
                }
                catch
                {
                    // If ToString throws an exception then the tag is ignored.
                    LogUnsupportedAttributeType?.Invoke(tag.Value.GetType().ToString(), tag.Key);
                    result = default;
                    return false;
                }

                break;
        }

        return true;
    }

    protected abstract T TransformIntegralTag(string key, long value);

    protected abstract T TransformFloatingPointTag(string key, double value);

    protected abstract T TransformBooleanTag(string key, bool value);

    protected abstract T TransformStringTag(string key, string value);

    protected abstract T TransformArrayTag(string key, Array array);

    private static string TruncateString(string value, int? maxLength)
    {
        return maxLength.HasValue && value?.Length > maxLength
            ? value.Substring(0, maxLength.Value)
            : value;
    }

    private T TransformArrayTagInternal(string key, Array array, int? maxStringValueLength)
    {
        // This switch ensures the values of the resultant array-valued tag are of the same type.
        return array switch
        {
            char[] => this.TransformArrayTag(key, array),
            string[] => this.ConvertToStringArrayThenTransformArrayTag(key, array, maxStringValueLength),
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
            _ => this.ConvertToStringArrayThenTransformArrayTag(key, array, maxStringValueLength),
        };
    }

    private T ConvertToStringArrayThenTransformArrayTag(string key, Array array, int? maxStringValueLength)
    {
        string[] stringArray;

        if (array is string[] arrayAsStringArray && (!maxStringValueLength.HasValue || !arrayAsStringArray.Any(s => s?.Length > maxStringValueLength)))
        {
            stringArray = arrayAsStringArray;
        }
        else
        {
            stringArray = new string[array.Length];
            for (var i = 0; i < array.Length; ++i)
            {
                stringArray[i] = TruncateString(array.GetValue(i)?.ToString(), maxStringValueLength);
            }
        }

        return this.TransformArrayTag(key, stringArray);
    }
}
