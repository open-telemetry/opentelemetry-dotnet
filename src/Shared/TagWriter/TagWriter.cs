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
        => this.TryWriteTag(ref state, tag.Key, tag.Value, tagValueMaxLength);

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
            case byte b:
                this.WriteIntegralTag(ref state, key, b);
                break;
            case sbyte sb:
                this.WriteIntegralTag(ref state, key, sb);
                break;
            case short s:
                this.WriteIntegralTag(ref state, key, s);
                break;
            case ushort us:
                this.WriteIntegralTag(ref state, key, us);
                break;
            case int i:
                this.WriteIntegralTag(ref state, key, i);
                break;
            case uint ui:
                this.WriteIntegralTag(ref state, key, ui);
                break;
            case long l:
                this.WriteIntegralTag(ref state, key, l);
                break;
            case float f:
                this.WriteFloatingPointTag(ref state, key, f);
                break;
            case double d:
                this.WriteFloatingPointTag(ref state, key, d);
                break;
            case Array array:
                if (value.GetType() == typeof(byte[]) && this.TryWriteByteArrayTag(ref state, key, ((byte[])value).AsSpan()))
                {
                    return true;
                }

                try
                {
                    this.WriteArrayTagInternal(ref state, key, array, tagValueMaxLength);
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
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

    protected abstract bool TryWriteByteArrayTag(ref TTagState state, string key, ReadOnlySpan<byte> value);

    protected abstract void WriteIntegralTag(ref TTagState state, string key, long value);

    protected abstract void WriteFloatingPointTag(ref TTagState state, string key, double value);

    protected abstract void WriteBooleanTag(ref TTagState state, string key, bool value);

    protected abstract void WriteStringTag(ref TTagState state, string key, ReadOnlySpan<char> value);

    protected abstract void WriteArrayTag(ref TTagState state, string key, ref TArrayState value);

    protected abstract void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName);

    private static ReadOnlySpan<char> TruncateString(ReadOnlySpan<char> value, int? maxLength)
        => maxLength.HasValue && value.Length > maxLength
           ? value.Slice(0, maxLength.Value)
           : value;

    private void WriteCharTag(ref TTagState state, string key, char value)
    {
        Span<char> destination = [value];
        this.WriteStringTag(ref state, key, destination);
    }

    private void WriteCharValue(ref TArrayState state, char value)
    {
        Span<char> destination = [value];
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
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
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
                case byte b:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, b);
                    break;
                case sbyte sb:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, sb);
                    break;
                case short s:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, s);
                    break;
                case ushort us:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, us);
                    break;
                case int intValue:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, intValue);
                    break;
                case uint ui:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, ui);
                    break;
                case long l:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, l);
                    break;
                case float f:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, f);
                    break;
                case double d:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, d);
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

    private void WriteStructToArray(ref TArrayState arrayState, char[] array)
    {
        foreach (var item in array)
        {
            this.WriteCharValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, bool[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteBooleanValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, sbyte[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, byte[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, short[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, ushort[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, int[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, uint[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, long[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteIntegralValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, float[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteFloatingPointValue(ref arrayState, item);
        }
    }

    private void WriteStructToArray(ref TArrayState arrayState, double[] array)
    {
        foreach (var item in array)
        {
            this.arrayWriter.WriteFloatingPointValue(ref arrayState, item);
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
        this.OnUnsupportedTagDropped(key, value.GetType().ToString());
        return false;
    }
}
