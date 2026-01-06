// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace OpenTelemetry.Internal;

internal abstract class TagWriter<TTagState, TArrayState, TKvlistState>
    where TTagState : notnull
    where TArrayState : notnull
    where TKvlistState : notnull
{
    private readonly ArrayTagWriter<TArrayState> arrayWriter;
    private readonly KvlistTagWriter<TKvlistState>? kvlistWriter;

    protected TagWriter(
        ArrayTagWriter<TArrayState> arrayTagWriter,
        KvlistTagWriter<TKvlistState>? kvlistTagWriter = null)
    {
        Guard.ThrowIfNull(arrayTagWriter);

        this.arrayWriter = arrayTagWriter;
        this.kvlistWriter = kvlistTagWriter;
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
                this.WriteIntegralTag(ref state, key, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case float:
            case double:
                this.WriteFloatingPointTag(ref state, key, Convert.ToDouble(value, CultureInfo.InvariantCulture));
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

            case IDictionary<string, object?> dict when this.kvlistWriter != null:
                try
                {
                    this.WriteKvlistTagInternal(ref state, key, dict, tagValueMaxLength);
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
                {
                    throw;
                }
                catch
                {
                    return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
                }

                break;

            case IReadOnlyDictionary<string, object?> dict when this.kvlistWriter != null:
                try
                {
                    this.WriteKvlistTagInternal(ref state, key, dict, tagValueMaxLength);
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
                {
                    throw;
                }
                catch
                {
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

    protected virtual void WriteKvlistTag(ref TTagState state, string key, ref TKvlistState value)
    {
        // Default implementation does nothing - subclasses that support kvlist should override
    }

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

    private void WriteKvlistTagInternal(ref TTagState state, string key, IEnumerable<KeyValuePair<string, object?>> dict, int? tagValueMaxLength)
    {
        Debug.Assert(this.kvlistWriter != null, "kvlistWriter was null");

        var kvlistState = this.kvlistWriter!.BeginWriteKvlist();

        try
        {
            foreach (var kvp in dict)
            {
                this.WriteKvlistEntry(ref kvlistState, kvp.Key, kvp.Value, tagValueMaxLength);
            }

            this.kvlistWriter.EndWriteKvlist(ref kvlistState);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
        {
            if (this.kvlistWriter.TryResize())
            {
                this.WriteKvlistTagInternal(ref state, key, dict, tagValueMaxLength);
                return;
            }

            this.WriteStringTag(
                ref state,
                key,
                "TRUNCATED".AsSpan());

            this.LogUnsupportedTagTypeAndReturnFalse(key, dict.GetType().ToString());
            return;
        }

        this.WriteKvlistTag(ref state, key, ref kvlistState);
    }

    private void WriteKvlistEntry(ref TKvlistState state, string key, object? value, int? tagValueMaxLength)
    {
        Debug.Assert(this.kvlistWriter != null, "kvlistWriter was null");

        if (value == null)
        {
            this.kvlistWriter!.WriteNullValue(ref state, key);
            return;
        }

        switch (value)
        {
            case char c:
                Span<char> charSpan = [c];
                this.kvlistWriter!.WriteStringValue(ref state, key, charSpan);
                break;
            case string s:
                this.kvlistWriter!.WriteStringValue(ref state, key, TruncateString(s.AsSpan(), tagValueMaxLength));
                break;
            case bool b:
                this.kvlistWriter!.WriteBooleanValue(ref state, key, b);
                break;
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
                this.kvlistWriter!.WriteIntegralValue(ref state, key, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case float:
            case double:
                this.kvlistWriter!.WriteFloatingPointValue(ref state, key, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case Array array:
                // Write nested array as ArrayValue within the kvlist
                var arrayState = this.arrayWriter.BeginWriteArray();
                this.WriteArrayToStateTypeChecked(ref arrayState, array, tagValueMaxLength);
                this.arrayWriter.EndWriteArray(ref arrayState);
                this.kvlistWriter!.WriteArrayValue(ref state, key, ref arrayState);
                break;
            case IDictionary<string, object?> nestedDict:
                // Recursively write nested dictionaries as KeyValueList
                var nestedKvlistState = this.kvlistWriter!.BeginWriteKvlist();
                foreach (var kvp in nestedDict)
                {
                    this.WriteKvlistEntry(ref nestedKvlistState, kvp.Key, kvp.Value, tagValueMaxLength);
                }

                this.kvlistWriter.EndWriteKvlist(ref nestedKvlistState);
                this.kvlistWriter.WriteKvlistValue(ref state, key, ref nestedKvlistState);
                break;
            case IReadOnlyDictionary<string, object?> nestedDict:
                // Recursively write nested dictionaries as KeyValueList
                var nestedKvlistState2 = this.kvlistWriter!.BeginWriteKvlist();
                foreach (var kvp in nestedDict)
                {
                    this.WriteKvlistEntry(ref nestedKvlistState2, kvp.Key, kvp.Value, tagValueMaxLength);
                }

                this.kvlistWriter.EndWriteKvlist(ref nestedKvlistState2);
                this.kvlistWriter.WriteKvlistValue(ref state, key, ref nestedKvlistState2);
                break;
            default:
                // Fall back to string conversion for unsupported types
                var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (stringValue != null)
                {
                    this.kvlistWriter!.WriteStringValue(ref state, key, TruncateString(stringValue.AsSpan(), tagValueMaxLength));
                }
                else
                {
                    this.kvlistWriter!.WriteNullValue(ref state, key);
                }

                break;
        }
    }

    private void WriteArrayToStateTypeChecked(ref TArrayState arrayState, Array array, int? tagValueMaxLength)
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
                    this.arrayWriter.WriteStringValue(ref arrayState, TruncateString(s.AsSpan(), tagValueMaxLength));
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
                    this.arrayWriter.WriteIntegralValue(ref arrayState, Convert.ToInt64(item, CultureInfo.InvariantCulture));
                    break;
                case float:
                case double:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, Convert.ToDouble(item, CultureInfo.InvariantCulture));
                    break;
                default:
                    var stringValue = Convert.ToString(item, CultureInfo.InvariantCulture);
                    if (stringValue == null)
                    {
                        this.arrayWriter.WriteNullValue(ref arrayState);
                    }
                    else
                    {
                        this.arrayWriter.WriteStringValue(ref arrayState, TruncateString(stringValue.AsSpan(), tagValueMaxLength));
                    }

                    break;
            }
        }
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
                    this.arrayWriter.WriteIntegralValue(ref arrayState, Convert.ToInt64(item, CultureInfo.InvariantCulture));
                    break;
                case float:
                case double:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, Convert.ToDouble(item, CultureInfo.InvariantCulture));
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
