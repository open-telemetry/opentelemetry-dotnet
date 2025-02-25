// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
        ref TTagState state,
        KeyValuePair<string, object?> tag,
        int? tagValueMaxLength = null)
    {
        return this.TryWriteTag(ref state, tag.Key, tag.Value, tagValueMaxLength);
    }

    public bool TryWriteTag(
        ref TTagState state,
        string key,
        object? value,
        int? tagValueMaxLength = null)
    {
        if (value == null)
        {
            return this.TryWriteEmptyTag(ref state, key, value);
        }

        switch (value)
        {
            case char c:
                this.WriteCharTag(ref state, key, c);
                break;
            case string s:
                this.WriteStringTag(
                    ref state,
                    key,
                    TruncateString(s.AsSpan(), tagValueMaxLength));
                break;
            case bool b:
                this.WriteBooleanTag(ref state, key, b);
                break;
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                this.WriteIntegralTag(ref state, key, Convert.ToInt64(value));
                break;
            case float:
            case double:
                this.WriteFloatingPointTag(ref state, key, Convert.ToDouble(value));
                break;
            case Array array:
                try
                {
                    this.WriteArrayTagInternal(ref state, key, array, tagValueMaxLength);
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
                {
                    throw;
                }
                catch
                {
                    // If an exception is thrown when calling ToString
                    // on any element of the array, then the entire array value
                    // is ignored.
                    return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
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
                    var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (stringValue == null)
                    {
                        return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
                    }

                    this.WriteStringTag(
                        ref state,
                        key,
                        TruncateString(stringValue.AsSpan(), tagValueMaxLength));
                }
                catch
                {
                    // If ToString throws an exception then the tag is ignored.
                    return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
                }

                break;
        }

        return true;
    }

    protected abstract bool TryWriteEmptyTag(ref TTagState state, string key, object? value);

    protected abstract void WriteIntegralTag(ref TTagState state, string key, long value);

    protected abstract void WriteFloatingPointTag(ref TTagState state, string key, double value);

    protected abstract void WriteBooleanTag(ref TTagState state, string key, bool value);

    protected abstract void WriteStringTag(ref TTagState state, string key, ReadOnlySpan<char> value);

    protected abstract void WriteArrayTag(ref TTagState state, string key, ref TArrayState value);

    protected abstract void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName);

    private static ReadOnlySpan<char> TruncateString(ReadOnlySpan<char> value, int? maxLength)
    {
        return maxLength.HasValue && value.Length > maxLength
            ? value.Slice(0, maxLength.Value)
            : value;
    }

    private void WriteCharTag(ref TTagState state, string key, char value)
    {
        Span<char> destination = stackalloc char[1];
        destination[0] = value;
        this.WriteStringTag(ref state, key, destination);
    }

    private void WriteCharValue(ref TArrayState state, char value)
    {
        Span<char> destination = stackalloc char[1];
        destination[0] = value;
        this.arrayWriter.WriteStringValue(ref state, destination);
    }

    private void WriteArrayTagInternal(ref TTagState state, string key, Array array, int? tagValueMaxLength)
    {
        var arrayState = this.arrayWriter.BeginWriteArray();

        try
        {
            // This switch ensures the values of the resultant array-valued tag are of the same type.
            switch (array)
            {
                case char[] charArray: this.WriteStructToArray(ref arrayState, charArray); break;
                case string?[] stringArray: this.WriteStringsToArray(ref arrayState, stringArray, tagValueMaxLength); break;
                case bool[] boolArray: this.WriteStructToArray(ref arrayState, boolArray); break;
                case byte[] byteArray: this.WriteToArrayCovariant(ref arrayState, byteArray); break;
                case short[] shortArray: this.WriteToArrayCovariant(ref arrayState, shortArray); break;
#if NETFRAMEWORK
                case int[]: this.WriteArrayTagIntNetFramework(ref arrayState, array, tagValueMaxLength); break;
                case long[]: this.WriteArrayTagLongNetFramework(ref arrayState, array, tagValueMaxLength); break;
#else
                case int[] intArray: this.WriteToArrayCovariant(ref arrayState, intArray); break;
                case long[] longArray: this.WriteToArrayCovariant(ref arrayState, longArray); break;
#endif
                case float[] floatArray: this.WriteStructToArray(ref arrayState, floatArray); break;
                case double[] doubleArray: this.WriteStructToArray(ref arrayState, doubleArray); break;
                default: this.WriteToArrayTypeChecked(ref arrayState, array, tagValueMaxLength); break;
            }

            this.arrayWriter.EndWriteArray(ref arrayState);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
        {
            // If the array writer cannot be resized, TryResize should log a message to the event source, return false.
            if (this.arrayWriter.TryResize())
            {
                this.WriteArrayTagInternal(ref state, key, array, tagValueMaxLength);
                return;
            }

            // Drop the array value and set "TRUNCATED" as value for easier isolation.
            // This is a best effort to avoid dropping the entire tag.
            this.WriteStringTag(
                ref state,
                key,
                "TRUNCATED".AsSpan());

            this.LogUnsupportedTagTypeAndReturnFalse(key, array.GetType().ToString());
            return;
        }

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
            this.WriteToArrayTypeChecked(ref arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArrayCovariant(ref arrayState, (int[])array);
    }

    private void WriteArrayTagLongNetFramework(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        // Note: On .NET Framework x64 nint[] & nuint[] fall into long[] case

        var arrayType = array.GetType();
        if (arrayType == typeof(nint[])
            || arrayType == typeof(nuint[]))
        {
            this.WriteToArrayTypeChecked(ref arrayState, array, tagValueMaxLength);
            return;
        }

        this.WriteToArrayCovariant(ref arrayState, (long[])array);
    }
#endif

    private void WriteToArrayTypeChecked(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
    {
        for (var i = 0; i < array.Length; ++i)
        {
            var item = array.GetValue(i);
            if (item == null)
            {
                this.arrayWriter.WriteNullValue(ref arrayState);
                continue;
            }

            switch (item)
            {
                case char c:
                    this.WriteCharValue(ref arrayState, c);
                    break;
                case string s:
                    this.arrayWriter.WriteStringValue(
                        ref arrayState,
                        TruncateString(s.AsSpan(), tagValueMaxLength));
                    break;
                case bool b:
                    this.arrayWriter.WriteBooleanValue(ref arrayState, b);
                    break;
                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, Convert.ToInt64(item));
                    break;
                case float:
                case double:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, Convert.ToDouble(item));
                    break;

                // All other types are converted to strings including the following
                // built-in value types:
                // case Array:   Nested array.
                // case nint:    Pointer type.
                // case nuint:   Pointer type.
                // case ulong:   May throw an exception on overflow.
                // case decimal: Converting to double produces rounding errors.
                default:
                    var stringValue = Convert.ToString(item, CultureInfo.InvariantCulture);
                    if (stringValue == null)
                    {
                        this.arrayWriter.WriteNullValue(ref arrayState);
                    }
                    else
                    {
                        this.arrayWriter.WriteStringValue(
                            ref arrayState,
                            TruncateString(stringValue.AsSpan(), tagValueMaxLength));
                    }

                    break;
            }
        }
    }

    private void WriteToArrayCovariant<TItem>(ref TArrayState arrayState, TItem[] array)
        where TItem : struct
    {
        // Note: The runtime treats int[]/uint[], byte[]/sbyte[],
        // short[]/ushort[], and long[]/ulong[] as covariant.

        if (typeof(TItem) == typeof(byte))
        {
            if (array.GetType() == typeof(sbyte[]))
            {
                this.WriteStructToArray(ref arrayState, (sbyte[])(object)array);
            }
            else
            {
                this.WriteStructToArray(ref arrayState, (byte[])(object)array);
            }
        }
        else if (typeof(TItem) == typeof(short))
        {
            if (array.GetType() == typeof(ushort[]))
            {
                this.WriteStructToArray(ref arrayState, (ushort[])(object)array);
            }
            else
            {
                this.WriteStructToArray(ref arrayState, (short[])(object)array);
            }
        }
        else if (typeof(TItem) == typeof(int))
        {
            if (array.GetType() == typeof(uint[]))
            {
                this.WriteStructToArray(ref arrayState, (uint[])(object)array);
            }
            else
            {
                this.WriteStructToArray(ref arrayState, (int[])(object)array);
            }
        }
        else if (typeof(TItem) == typeof(long))
        {
            if (array.GetType() == typeof(ulong[]))
            {
                this.WriteToArrayTypeChecked(ref arrayState, array, tagValueMaxLength: null);
            }
            else
            {
                this.WriteStructToArray(ref arrayState, (long[])(object)array);
            }
        }
        else
        {
            Debug.Fail("Unexpected type encountered");

            throw new NotSupportedException();
        }
    }

    private void WriteStructToArray<TItem>(ref TArrayState arrayState, TItem[] array)
        where TItem : struct
    {
        foreach (TItem item in array)
        {
            if (typeof(TItem) == typeof(char))
            {
                this.WriteCharValue(ref arrayState, (char)(object)item);
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

    private void WriteStringsToArray(ref TArrayState arrayState, string?[] array, int? tagValueMaxLength)
    {
        foreach (var item in array)
        {
            if (item == null)
            {
                this.arrayWriter.WriteNullValue(ref arrayState);
            }
            else
            {
                this.arrayWriter.WriteStringValue(
                    ref arrayState,
                    TruncateString(item.AsSpan(), tagValueMaxLength));
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
