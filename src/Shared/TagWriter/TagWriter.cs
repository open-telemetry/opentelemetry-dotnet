// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

internal abstract class TagWriter<TTagState, TArrayState>
    where TTagState : notnull
    where TArrayState : notnull
{
    internal const int MaxRecursionDepth = 3; // TODO https://github.com/open-telemetry/semantic-conventions/issues/3648

    [ThreadStatic]
    private static int recursionDepth;

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
            // Ordered by how often each type is likely to appear for performance
            case string s:
                if (tagValueMaxLength is { } length && s.Length > length)
                {
                    this.WriteStringTag(
                        ref state,
                        key,
                        s.AsSpan(0, length));
                }
                else
                {
                    this.WriteStringTag(ref state, key, s);
                }

                break;
            case int i:
                this.WriteIntegralTag(ref state, key, i);
                break;
            case long l:
                this.WriteIntegralTag(ref state, key, l);
                break;
            case bool b:
                this.WriteBooleanTag(ref state, key, b);
                break;
            case double d:
                this.WriteFloatingPointTag(ref state, key, d);
                break;
            case char c:
                this.WriteCharTag(ref state, key, c);
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
            case uint ui:
                this.WriteIntegralTag(ref state, key, ui);
                break;
            case float f:
                this.WriteFloatingPointTag(ref state, key, f);
                break;
            case IEnumerable<KeyValuePair<string, object?>> kvList:
                return this.TryWriteKvListTag(ref state, key, value, kvList, tagValueMaxLength);

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

            case IEnumerable<KeyValuePair<string, string?>> stringKvList:
                return this.TryWriteKvListTag(ref state, key, value, stringKvList, tagValueMaxLength);

            case IDictionary dictionary:
                return this.TryWriteKvListTag(ref state, key, value, dictionary, tagValueMaxLength);

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
                        stringValue,
                        tagValueMaxLength);
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

    protected virtual void WriteStringTag(ref TTagState state, string key, string value)
        => this.WriteStringTag(ref state, key, value.AsSpan());

    protected abstract void WriteStringTag(ref TTagState state, string key, ReadOnlySpan<char> value);

    protected abstract void WriteArrayTag(ref TTagState state, string key, ref TArrayState value);

    protected abstract void WriteKvListTag<TEnumerator>(ref TTagState state, string key, ref TEnumerator kvList, int? tagValueMaxLength)
        where TEnumerator : struct, IKeyValueListEnumerator;

    protected abstract void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName);

    private static ReadOnlySpan<char> TruncateString(ReadOnlySpan<char> value, int? maxLength)
        => maxLength is { } maxLengthValue && value.Length > maxLengthValue
           ? value.Slice(0, maxLengthValue)
           : value;

    // Note: Enumerator selection is done here rather than in TryWriteTag to
    // keep it out of the frame of the hot path which every tag goes through.
    // The shapes which are seen most often get a specialized enumerator so
    // that writing their entries does not allocate.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryWriteKvListTag(
        ref TTagState state,
        string key,
        object value,
        IEnumerable<KeyValuePair<string, object?>> kvList,
        int? tagValueMaxLength)
    {
        switch (kvList)
        {
            case Dictionary<string, object?> dictionary:
            {
                var enumerator = new ObjectDictionaryKeyValueListEnumerator(dictionary);
                return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
            }

            case List<KeyValuePair<string, object?>> list:
            {
                var enumerator = new ListKeyValueListEnumerator(list);
                return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
            }

            case KeyValuePair<string, object?>[] array:
            {
                var enumerator = new ArrayKeyValueListEnumerator(array);
                return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
            }

            default:
            {
                var enumerator = new ObjectEnumerableKeyValueListEnumerator(kvList);
                return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryWriteKvListTag(
        ref TTagState state,
        string key,
        object value,
        IEnumerable<KeyValuePair<string, string?>> kvList,
        int? tagValueMaxLength)
    {
        if (kvList is Dictionary<string, string?> dictionary)
        {
            var enumerator = new StringDictionaryKeyValueListEnumerator(dictionary);
            return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
        }
        else
        {
            var enumerator = new StringEnumerableKeyValueListEnumerator(kvList);
            return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryWriteKvListTag(
        ref TTagState state,
        string key,
        object value,
        IDictionary kvList,
        int? tagValueMaxLength)
    {
        var enumerator = new DictionaryKeyValueListEnumerator(kvList);
        return this.TryWriteKvListTagWithinDepthLimit(ref state, key, value, ref enumerator, tagValueMaxLength);
    }

    private bool TryWriteKvListTagWithinDepthLimit<TEnumerator>(
        ref TTagState state,
        string key,
        object value,
        ref TEnumerator kvList,
        int? tagValueMaxLength)
        where TEnumerator : struct, IKeyValueListEnumerator
    {
        if (recursionDepth >= MaxRecursionDepth)
        {
            // The nesting limit has been reached so the value is
            // written as a string instead of recursing any further.
            // This branch does not take part in the recursion so it
            // must not touch the depth.
            try
            {
                var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                this.WriteStringTag(
                    ref state,
                    key,
                    TruncateString(stringValue.AsSpan(), tagValueMaxLength));
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                recursionDepth = 0;
                kvList.Dispose();
                throw;
            }
            catch
            {
                // If ToString throws an exception then the tag is ignored.
                kvList.Dispose();
                return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
            }

            kvList.Dispose();
            return true;
        }

        recursionDepth++;

        try
        {
            this.WriteKvListTag(ref state, key, ref kvList, tagValueMaxLength);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
        {
            recursionDepth = 0;
            kvList.Dispose();
            throw;
        }
        catch
        {
            recursionDepth--;
            kvList.Dispose();
            return this.LogUnsupportedTagTypeAndReturnFalse(key, value);
        }

        recursionDepth--;
        kvList.Dispose();

        return true;
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
        // Upper bound on the number of times the array serialization buffer is grown before giving up. Each
        // retry at least doubles the buffer, which is itself capped by the ArrayTagWriter's own  maximum size.
        const int MaxArrayTagBufferGrowthAttempts = 32;

        var arrayState = this.arrayWriter.BeginWriteArray();

        try
        {
            // The buffer at least doubles on each retry and TryResize returns false once it hits
            // its own maximum size, so the loop is already bounded. This explicit attempt cap is a
            // guard that guarantees termination even if a write were to keep faulting without the
            // buffer actually being too small.
            var written = false;
            for (var attempt = 0; attempt < MaxArrayTagBufferGrowthAttempts; attempt++)
            {
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
                    written = true;
                    break;
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                {
                    // If the array writer cannot be resized, TryResize should log a message to the event source, return false.
                    if (this.arrayWriter.TryResize(ref arrayState))
                    {
                        continue;
                    }

                    this.arrayWriter.AbortWriteArray(ref arrayState);

                    // Drop the array value and set "TRUNCATED" as value for easier isolation.
                    // This is a best effort to avoid dropping the entire tag.
                    this.WriteStringTag(
                        ref state,
                        key,
                        "TRUNCATED".AsSpan());

                    this.LogUnsupportedTagTypeAndReturnFalse(key, array.GetType().ToString());
                    return;
                }
            }

            if (!written)
            {
                ThrowIfTooManyRetries();
            }

            this.WriteArrayTag(ref state, key, ref arrayState);
        }
        catch
        {
            this.arrayWriter.AbortWriteArray(ref arrayState);
            throw;
        }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        static void ThrowIfTooManyRetries()
        {
            throw new InvalidOperationException("The array-valued tag could not be written within the maximum number of buffer-growth attempts.");
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

            // Ordered by how often each type is likely to appear for performance
            switch (item)
            {
                case string s:
                    this.WriteStringValue(
                        ref arrayState,
                        s,
                        tagValueMaxLength);
                    break;
                case int intValue:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, intValue);
                    break;
                case long l:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, l);
                    break;
                case bool b:
                    this.arrayWriter.WriteBooleanValue(ref arrayState, b);
                    break;
                case double d:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, d);
                    break;
                case char c:
                    this.WriteCharValue(ref arrayState, c);
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
                case uint ui:
                    this.arrayWriter.WriteIntegralValue(ref arrayState, ui);
                    break;
                case float f:
                    this.arrayWriter.WriteFloatingPointValue(ref arrayState, f);
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
                        this.WriteStringValue(
                            ref arrayState,
                            stringValue,
                            tagValueMaxLength);
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
                this.WriteStringValue(
                    ref arrayState,
                    item,
                    tagValueMaxLength);
            }
        }
    }

    private void WriteStringTag(ref TTagState state, string key, string value, int? maxLength)
    {
        if (maxLength is { } length && value.Length > length)
        {
            this.WriteStringTag(ref state, key, value.AsSpan(0, length));
        }
        else
        {
            this.WriteStringTag(ref state, key, value);
        }
    }

    private void WriteStringValue(ref TArrayState state, string value, int? maxLength)
    {
        if (maxLength is { } length && value.Length > length)
        {
            this.arrayWriter.WriteStringValue(ref state, value.AsSpan(0, length));
        }
        else
        {
            this.arrayWriter.WriteStringValue(ref state, value);
        }
    }

    private bool LogUnsupportedTagTypeAndReturnFalse(string key, object value)
    {
        this.OnUnsupportedTagDropped(key, value.GetType().ToString());
        return false;
    }
}
