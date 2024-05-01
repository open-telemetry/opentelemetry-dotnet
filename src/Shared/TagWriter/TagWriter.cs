// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
        ref TTagState state,
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
                this.WriteStringTag(ref state, tag.Key, TruncateString(Convert.ToString(tag.Value)!, tagValueMaxLength));
                break;
            case bool b:
                this.WriteBooleanTag(ref state, tag.Key, b);
                break;
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                this.WriteIntegralTag(ref state, tag.Key, Convert.ToInt64(tag.Value));
                break;
            case float:
            case double:
                this.WriteFloatingPointTag(ref state, tag.Key, Convert.ToDouble(tag.Value));
                break;
            case Array array:
                try
                {
                    this.WriteArrayTagInternal(ref state, tag.Key, array, tagValueMaxLength);
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
                    var stringValue = TruncateString(Convert.ToString(tag.Value/*TODO: , CultureInfo.InvariantCulture*/), tagValueMaxLength);
                    if (stringValue == null)
                    {
                        return this.LogUnsupportedTagTypeAndReturnFalse(tag.Key, tag.Value);
                    }

                    this.WriteStringTag(ref state, tag.Key, stringValue);
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

    protected abstract void WriteIntegralTag(ref TTagState state, string key, long value);

    protected abstract void WriteFloatingPointTag(ref TTagState state, string key, double value);

    protected abstract void WriteBooleanTag(ref TTagState state, string key, bool value);

    protected abstract void WriteStringTag(ref TTagState state, string key, string value);

    protected abstract void WriteArrayTag(ref TTagState state, string key, ref TArrayState value);

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

    private void WriteArrayTagInternal(ref TTagState state, string key, Array array, int? tagValueMaxLength)
    {
        var arrayState = this.arrayWriter.BeginWriteArray();

        // This switch ensures the values of the resultant array-valued tag are of the same type.
        switch (array)
        {
            case char[] charArray: this.WriteToArray(ref arrayState, charArray); break;
            case string[]: this.ConvertToStringArrayThenWriteArrayTag(ref arrayState, array, tagValueMaxLength); break;
            case bool[] boolArray: this.WriteToArray(ref arrayState, boolArray); break;
            case byte[] byteArray: this.WriteToArray(ref arrayState, byteArray); break;
            case sbyte[] sbyteArray: this.WriteToArray(ref arrayState, sbyteArray); break;
            case short[] shortArray: this.WriteToArray(ref arrayState, shortArray); break;
            case ushort[] ushortArray: this.WriteToArray(ref arrayState, ushortArray); break;
            case uint[] uintArray: this.WriteToArray(ref arrayState, uintArray); break;
#if NETFRAMEWORK
            case int[]: this.WriteArrayTagIntNetFramework(ref arrayState, array, tagValueMaxLength); break;
            case long[]: this.WriteArrayTagLongNetFramework(ref arrayState, array, tagValueMaxLength); break;
#else
            case int[] intArray: this.WriteToArray(ref arrayState, intArray); break;
            case long[] longArray: this.WriteToArray(ref arrayState, longArray); break;
#endif
            case float[] floatArray: this.WriteToArray(ref arrayState, floatArray); break;
            case double[] doubleArray: this.WriteToArray(ref arrayState, doubleArray); break;
            default: this.ConvertToStringArrayThenWriteArrayTag(ref arrayState, array, tagValueMaxLength); break;
        }

        this.arrayWriter.EndWriteArray(ref arrayState);

        this.WriteArrayTag(ref state, key, ref arrayState);
    }

#if NETFRAMEWORK
    private void WriteArrayTagIntNetFramework(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        // Note: On .NET Framework x86 nint[] & nuint[] fall into int[] case

        var arrayType = array.GetType();
        if (arrayType == typeof(nint[])
            || arrayType == typeof(nuint[]))
        {
            this.ConvertToStringArrayThenWriteArrayTag(ref arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArray(ref arrayState, (int[])array);
    }

    private void WriteArrayTagLongNetFramework(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        // Note: On .NET Framework x64 nint[] & nuint[] fall into long[] case

        var arrayType = array.GetType();
        if (arrayType == typeof(nint[])
            || arrayType == typeof(nuint[]))
        {
            this.ConvertToStringArrayThenWriteArrayTag(ref arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArray(ref arrayState, (long[])array);
    }
#endif

    private void ConvertToStringArrayThenWriteArrayTag(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        if (array is string?[] arrayAsStringArray
            && (!tagValueMaxLength.HasValue || !arrayAsStringArray.Any(s => s?.Length > tagValueMaxLength)))
        {
            this.WriteStringsToArray(ref arrayState, arrayAsStringArray);
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
                        : TruncateString(Convert.ToString(item/*TODO: , CultureInfo.InvariantCulture*/), tagValueMaxLength);
                }

                this.WriteStringsToArray(ref arrayState, new(stringArray, 0, array.Length));
            }
            finally
            {
                ArrayPool<string?>.Shared.Return(stringArray);
            }
        }
    }

    private void WriteToArray<TItem>(ref TArrayState arrayState, TItem[] array)
        where TItem : struct
    {
        foreach (TItem item in array)
        {
            if (typeof(TItem) == typeof(char))
            {
                this.arrayWriter.WriteStringValue(ref arrayState, Convert.ToString((char)(object)item)!);
            }
            else if (typeof(TItem) == typeof(bool))
            {
                this.arrayWriter.WriteBooleanValue(ref arrayState, (bool)(object)item);
            }
            else if (typeof(TItem) == typeof(byte))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (byte)(object)item);
            }
            else if (typeof(TItem) == typeof(sbyte))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (sbyte)(object)item);
            }
            else if (typeof(TItem) == typeof(short))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (short)(object)item);
            }
            else if (typeof(TItem) == typeof(ushort))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (ushort)(object)item);
            }
            else if (typeof(TItem) == typeof(int))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (int)(object)item);
            }
            else if (typeof(TItem) == typeof(uint))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (uint)(object)item);
            }
            else if (typeof(TItem) == typeof(long))
            {
                this.arrayWriter.WriteIntegralValue(ref arrayState, (long)(object)item);
            }
            else if (typeof(TItem) == typeof(float))
            {
                this.arrayWriter.WriteFloatingPointValue(ref arrayState, (float)(object)item);
            }
            else if (typeof(TItem) == typeof(double))
            {
                this.arrayWriter.WriteFloatingPointValue(ref arrayState, (double)(object)item);
            }
            else
            {
                Debug.Fail("Unexpected type encountered");

                throw new NotSupportedException();
            }
        }
    }

    private void WriteStringsToArray(ref TArrayState arrayState, ReadOnlySpan<string?> data)
    {
        foreach (var item in data)
        {
            if (item == null)
            {
                this.arrayWriter.WriteNullValue(ref arrayState);
            }
            else
            {
                this.arrayWriter.WriteStringValue(ref arrayState, item);
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
