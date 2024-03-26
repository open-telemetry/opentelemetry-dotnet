// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenTelemetry.Internal;

internal abstract class TagWriter<TTagState, TArrayState>
    where TTagState : notnull
    where TArrayState : notnull
{
    private readonly ArrayTagWriter<TArrayState> arrayWriter;

    protected TagWriter(
        ArrayTagWriter<TArrayState> arrayTagWriter)
    {
        Guard.ThrowIfNull(arrayTagWriter);

        this.arrayWriter = arrayTagWriter;
    }

    public bool TryWriteTag(
        TTagState state,
        KeyValuePair<string, object?> tag,
        int? tagValueMaxLength = null)
    {
        if (tag.Value == null)
        {
            return false;
        }

        switch (tag.Value)
        {
            case char:
            case string:
                this.WriteStringTag(state, tag.Key, TruncateString(Convert.ToString(tag.Value)!, tagValueMaxLength));
                break;
            case bool b:
                this.WriteBooleanTag(state, tag.Key, b);
                break;
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                this.WriteIntegralTag(state, tag.Key, Convert.ToInt64(tag.Value));
                break;
            case float:
            case double:
                this.WriteFloatingPointTag(state, tag.Key, Convert.ToDouble(tag.Value));
                break;
            case Array array:
                try
                {
                    this.WriteArrayTagInternal(state, tag.Key, array, tagValueMaxLength);
                }
                catch
                {
                    // If an exception is thrown when calling ToString
                    // on any element of the array, then the entire array value
                    // is ignored.
                    return this.LogUnsupportedTagTypeAndReturnFalse(tag.Key, tag.Value);
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
                    var stringValue = TruncateString(Convert.ToString(tag.Value, CultureInfo.InvariantCulture), tagValueMaxLength);
                    if (stringValue == null)
                    {
                        return this.LogUnsupportedTagTypeAndReturnFalse(tag.Key, tag.Value);
                    }

                    this.WriteStringTag(state, tag.Key, stringValue);
                }
                catch
                {
                    // If ToString throws an exception then the tag is ignored.
                    return this.LogUnsupportedTagTypeAndReturnFalse(tag.Key, tag.Value);
                }

                break;
        }

        return true;
    }

    protected abstract void WriteIntegralTag(TTagState state, string key, long value);

    protected abstract void WriteFloatingPointTag(TTagState state, string key, double value);

    protected abstract void WriteBooleanTag(TTagState state, string key, bool value);

    protected abstract void WriteStringTag(TTagState state, string key, string value);

    protected abstract void WriteArrayTag(TTagState state, string key, TArrayState value);

    protected abstract void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName);

    [return: NotNullIfNotNull(nameof(value))]
    private static string? TruncateString(string? value, int? maxLength)
    {
        return maxLength.HasValue && value?.Length > maxLength
            ? value.Substring(0, maxLength.Value)
            : value;
    }

    private void WriteArrayTagInternal(TTagState state, string key, Array array, int? tagValueMaxLength)
    {
        var arrayState = this.arrayWriter.BeginWriteArray();

        // This switch ensures the values of the resultant array-valued tag are of the same type.
        switch (array)
        {
            case char[] charArray: this.WriteToArray(arrayState, charArray); break;
            case string[]: this.ConvertToStringArrayThenWriteArrayTag(arrayState, array, tagValueMaxLength); break;
            case bool[] boolArray: this.WriteToArray(arrayState, boolArray); break;
            case byte[] byteArray: this.WriteToArray(arrayState, byteArray); break;
            case sbyte[] sbyteArray: this.WriteToArray(arrayState, sbyteArray); break;
            case short[] shortArray: this.WriteToArray(arrayState, shortArray); break;
            case ushort[] ushortArray: this.WriteToArray(arrayState, ushortArray); break;
#if NETFRAMEWORK
            case int[]: this.WriteArrayTagIntNetFramework(arrayState, array, tagValueMaxLength); break;
#else
            case int[] intArray: this.WriteToArray(arrayState, intArray); break;
#endif
            case uint[] uintArray: this.WriteToArray(arrayState, uintArray); break;
#if NETFRAMEWORK
            case long[]: this.WriteArrayTagLongNetFramework(arrayState, array, tagValueMaxLength); break;
#else
            case long[] longArray: this.WriteToArray(arrayState, longArray); break;
#endif
            case float[] floatArray: this.WriteToArray(arrayState, floatArray); break;
            case double[] doubleArray: this.WriteToArray(arrayState, doubleArray); break;
            default: this.ConvertToStringArrayThenWriteArrayTag(arrayState, array, tagValueMaxLength); break;
        }

        this.arrayWriter.EndWriteArray(arrayState);

        this.WriteArrayTag(state, key, arrayState);
    }

#if NETFRAMEWORK
    private void WriteArrayTagIntNetFramework(TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        // Note: On .NET Framework x86 nint[] & nuint[] fall into int[] case

        var arrayType = array.GetType();
        if (arrayType == typeof(nint[])
            || arrayType == typeof(nuint[]))
        {
            this.ConvertToStringArrayThenWriteArrayTag(arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArray(arrayState, (int[])array);
    }

    private void WriteArrayTagLongNetFramework(TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        // Note: On .NET Framework x64 nint[] & nuint[] fall into long[] case

        var arrayType = array.GetType();
        if (arrayType == typeof(nint[])
            || arrayType == typeof(nuint[]))
        {
            this.ConvertToStringArrayThenWriteArrayTag(arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArray(arrayState, (long[])array);
    }
#endif

    private void ConvertToStringArrayThenWriteArrayTag(TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        if (array is string?[] arrayAsStringArray
            && (!tagValueMaxLength.HasValue || !arrayAsStringArray.Any(s => s?.Length > tagValueMaxLength)))
        {
            this.WriteStringsToArray(arrayState, arrayAsStringArray);
        }
        else
        {
            string?[] stringArray = ArrayPool<string?>.Shared.Rent(array.Length);
            try
            {
                for (var i = 0; i < array.Length; ++i)
                {
                    var item = array.GetValue(i);
                    stringArray[i] = item == null
                        ? null
                        : TruncateString(Convert.ToString(item, CultureInfo.InvariantCulture), tagValueMaxLength);
                }

                this.WriteStringsToArray(arrayState, new(stringArray, 0, array.Length));
            }
            finally
            {
                ArrayPool<string?>.Shared.Return(stringArray);
            }
        }
    }

    private void WriteToArray<TItem>(TArrayState arrayState, TItem[] array)
        where TItem : struct
    {
        foreach (TItem item in array)
        {
            if (typeof(TItem) == typeof(char))
            {
                this.arrayWriter.WriteStringTag(arrayState, Convert.ToString((char)(object)item)!);
            }
            else if (typeof(TItem) == typeof(bool))
            {
                this.arrayWriter.WriteBooleanTag(arrayState, (bool)(object)item);
            }
            else if (typeof(TItem) == typeof(byte))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (byte)(object)item);
            }
            else if (typeof(TItem) == typeof(sbyte))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (sbyte)(object)item);
            }
            else if (typeof(TItem) == typeof(short))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (short)(object)item);
            }
            else if (typeof(TItem) == typeof(ushort))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (ushort)(object)item);
            }
            else if (typeof(TItem) == typeof(int))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (int)(object)item);
            }
            else if (typeof(TItem) == typeof(uint))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (uint)(object)item);
            }
            else if (typeof(TItem) == typeof(long))
            {
                this.arrayWriter.WriteIntegralTag(arrayState, (long)(object)item);
            }
            else if (typeof(TItem) == typeof(float))
            {
                this.arrayWriter.WriteFloatingPointTag(arrayState, (float)(object)item);
            }
            else if (typeof(TItem) == typeof(double))
            {
                this.arrayWriter.WriteFloatingPointTag(arrayState, (double)(object)item);
            }
            else
            {
                Debug.Fail("Unexpected type encountered");

                throw new NotSupportedException();
            }
        }
    }

    private void WriteStringsToArray(TArrayState arrayState, ReadOnlySpan<string?> data)
    {
        foreach (var item in data)
        {
            if (item == null)
            {
                this.arrayWriter.WriteNullTag(arrayState);
            }
            else
            {
                this.arrayWriter.WriteStringTag(arrayState, item);
            }
        }
    }

    private bool LogUnsupportedTagTypeAndReturnFalse(string key, object value)
    {
        Debug.Assert(value != null, "value was null");

        this.OnUnsupportedTagDropped(key, value!.GetType().ToString());
        return false;
    }
}
