// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// Extension methods to extract TraceState from string.
/// </summary>
internal static class TraceStateUtils
{
    private const int KeyMaxSize = 256;
    private const int ValueMaxSize = 256;
    private const int MaxKeyValuePairsCount = 32;
    private const int MaxTraceStateLength = 512;
    private const int LargeEntryLength = 128;

    /// <summary>
    /// Extracts tracestate pairs from the given string and appends it to provided tracestate list.
    /// </summary>
    /// <param name="traceStateString">String with comma separated tracestate key value pairs.</param>
    /// <param name="tracestate"><see cref="List{T}"/> to set tracestate pairs on.</param>
    /// <returns>True if string was parsed successfully and tracestate was recognized, false otherwise.</returns>
    internal static bool AppendTraceState(string traceStateString, List<KeyValuePair<string, string>> tracestate)
    {
        Debug.Assert(tracestate != null, "tracestate list cannot be null");

        if (string.IsNullOrEmpty(traceStateString))
        {
            return false;
        }

        var isValid = true;
        try
        {
            var names = new HashSet<string>();

            var traceStateSpan = traceStateString.AsSpan().Trim(' ').Trim(',');
            do
            {
                // tracestate: rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01,congo=BleGNlZWRzIHRohbCBwbGVhc3VyZS4

                var pairEnd = traceStateSpan.IndexOf(',');
                if (pairEnd < 0)
                {
                    pairEnd = traceStateSpan.Length;
                }

                if (!TryParseKeyValue(traceStateSpan.Slice(0, pairEnd), out var key, out var value))
                {
                    isValid = false;
                    break;
                }

                var keyStr = key.ToString();
                if (names.Add(keyStr))
                {
#if NET
                    tracestate.Add(new KeyValuePair<string, string>(keyStr, value.ToString()));
#else
                    tracestate!.Add(new KeyValuePair<string, string>(keyStr, value.ToString()));
#endif
                }
                else
                {
                    isValid = false;
                    break;
                }

                if (tracestate.Count > MaxKeyValuePairsCount)
                {
                    OpenTelemetryApiEventSource.Log.TooManyItemsInTracestate();
                    isValid = false;
                    break;
                }

                if (pairEnd == traceStateSpan.Length)
                {
                    break;
                }

                traceStateSpan = traceStateSpan.Slice(pairEnd + 1);
            }
            while (!traceStateSpan.IsEmpty);

            if (!isValid)
            {
#if NET
                tracestate.Clear();
#else
                tracestate!.Clear();
#endif
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            OpenTelemetryApiEventSource.Log.TracestateExtractException(ex);
        }

        return false;
    }

    internal static string GetString(IEnumerable<KeyValuePair<string, string>>? traceState)
    {
        if (traceState == null)
        {
            return string.Empty;
        }

        var entries = new List<string>();
        var pairsCount = 0;
        foreach (var entry in traceState)
        {
            pairsCount++;
            if (entries.Count < MaxKeyValuePairsCount)
            {
                // Take the first MaxKeyValuePairsCount pairs and ignore older pairs after that.
                entries.Add(string.Concat(entry.Key, "=", entry.Value));
            }
        }

        if (entries.Count == 0)
        {
            return string.Empty;
        }

        if (pairsCount > MaxKeyValuePairsCount)
        {
            OpenTelemetryApiEventSource.Log.TooManyItemsInTracestate();
        }

        TruncateEntries(entries);

        return string.Join(",", entries);
    }

    private static void TruncateEntries(List<string> entries)
    {
        if (GetCombinedLength(entries) <= MaxTraceStateLength)
        {
            return;
        }

        for (var i = entries.Count - 1; i >= 0 && GetCombinedLength(entries) > MaxTraceStateLength; i--)
        {
            if (entries[i].Length > LargeEntryLength)
            {
                entries.RemoveAt(i);
            }
        }

        while (entries.Count > 0 && GetCombinedLength(entries) > MaxTraceStateLength)
        {
            entries.RemoveAt(entries.Count - 1);
        }
    }

    private static bool TryParseKeyValue(ReadOnlySpan<char> pair, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
    {
        key = default;
        value = default;

        var keyEndIdx = pair.IndexOf('=');
        if (keyEndIdx <= 0)
        {
            return false;
        }

        var valueStartIdx = keyEndIdx + 1;
        if (valueStartIdx >= pair.Length)
        {
            return false;
        }

        key = pair.Slice(0, keyEndIdx).TrimStart();
        if (!ValidateKey(key))
        {
            OpenTelemetryApiEventSource.Log.TracestateKeyIsInvalid(key);
            return false;
        }

        value = pair.Slice(valueStartIdx).Trim();
        if (!ValidateValue(value))
        {
            OpenTelemetryApiEventSource.Log.TracestateValueIsInvalid(value);
            return false;
        }

        return true;
    }

    private static bool ValidateKey(ReadOnlySpan<char> key)
    {
        // https://www.w3.org/TR/trace-context-2/#key
        // key = (lcalpha / DIGIT) 0*255(keychar)
        // keychar = lcalpha / DIGIT / "_" / "-" / "*" / "/" / "@"
        if (key.IsEmpty
            || key.Length > KeyMaxSize
            || !IsValidFirstCharacter(key[0]))
        {
            return false;
        }

        for (var i = 1; i < key.Length; i++)
        {
            if (!IsValidCharacter(key[i]))
            {
                return false;
            }
        }

        return true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidFirstCharacter(char c)
            => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidCharacter(char c)
            => IsValidFirstCharacter(c) || c is '_' or '-' or '*' or '/' or '@';
    }

    private static bool ValidateValue(ReadOnlySpan<char> value)
    {
        // Value is opaque string up to 256 characters printable ASCII RFC0020 characters (i.e., the range
        // 0x20 to 0x7E) except comma , and =.

        if (value.Length > ValueMaxSize || value[value.Length - 1] == ' ' /* '\u0020' */)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is ',' or '=' or < ' ' /* '\u0020' */ or > '~' /* '\u007E' */)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetCombinedLength(List<string> entries)
    {
        var combinedLength = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0)
            {
                combinedLength++;
            }

            combinedLength += entries[i].Length;
        }

        return combinedLength;
    }
}
