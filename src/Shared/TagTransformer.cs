// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Internal;

internal abstract class TagTransformer<T>
    where T : notnull
{
    protected TagTransformer()
    {
    }

    public bool TryTransformTag(
        KeyValuePair<string, object> tag,
        [NotNullWhen(true)] out T? result,
        int? maxLength = null)
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
                result = this.TransformStringTag(tag.Key, TruncateString(Convert.ToString(tag.Value)!, maxLength));
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
                    return this.LogUnsupportedAttributeTypeAndReturnDefault(tag.Key, tag.Value, out result);
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
                    var stringValue = TruncateString(Convert.ToString(tag.Value), maxLength);
                    if (stringValue == null)
                    {
                        return this.LogUnsupportedAttributeTypeAndReturnDefault(tag.Key, tag.Value, out result);
                    }

                    result = this.TransformStringTag(tag.Key, stringValue);
                }
                catch
                {
                    // If ToString throws an exception then the tag is ignored.
                    return this.LogUnsupportedAttributeTypeAndReturnDefault(tag.Key, tag.Value, out result);
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

    protected abstract void OnUnsupportedAttributeDropped(
        string attributeKey,
        string attributeValueTypeFullName);

    [return: NotNullIfNotNull(nameof(value))]
    private static string? TruncateString(string? value, int? maxLength)
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
        string?[] stringArray;

        if (array is string?[] arrayAsStringArray && (!maxStringValueLength.HasValue || !arrayAsStringArray.Any(s => s?.Length > maxStringValueLength)))
        {
            stringArray = arrayAsStringArray;
        }
        else
        {
            stringArray = new string?[array.Length];
            for (var i = 0; i < array.Length; ++i)
            {
                var item = array.GetValue(i);
                stringArray[i] = item == null
                    ? null
                    : TruncateString(Convert.ToString(item), maxStringValueLength);
            }
        }

        return this.TransformArrayTag(key, stringArray);
    }

    private bool LogUnsupportedAttributeTypeAndReturnDefault(string key, object value, out T? result)
    {
        Debug.Assert(value != null, "value was null");

        this.OnUnsupportedAttributeDropped(key, value!.GetType().ToString());
        result = default;
        return false;
    }
}
