// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
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

        bool isValid = true;
        try
        {
            var names = new HashSet<string>();

            var traceStateSpan = traceStateString.AsSpan().Trim(' ').Trim(',');
            do
            {
                // tracestate: rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01,congo=BleGNlZWRzIHRohbCBwbGVhc3VyZS4

                int pairEnd = traceStateSpan.IndexOf(',');
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
                    tracestate!.Add(new KeyValuePair<string, string>(keyStr, value.ToString()));
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
                tracestate!.Clear();
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
#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection
        if (traceState == null || !traceState.Any())
        {
            return string.Empty;
        }

        // it's supposedly cheaper to iterate over very short collection a couple of times
        // than to convert it to array.
        var pairsCount = traceState.Count();
        if (pairsCount > MaxKeyValuePairsCount)
        {
            OpenTelemetryApiEventSource.Log.TooManyItemsInTracestate();
        }

        var sb = new StringBuilder();

        int ind = 0;
        foreach (var entry in traceState)
        {
            if (ind++ < MaxKeyValuePairsCount)
            {
                // take last MaxKeyValuePairsCount pairs, ignore last (oldest) pairs
                sb.Append(entry.Key)
                    .Append('=')
                    .Append(entry.Value)
                    .Append(',');
            }
        }
#pragma warning restore CA1851 // Possible multiple enumerations of 'IEnumerable' collection

        return sb.Remove(sb.Length - 1, 1).ToString();
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
        // Key is opaque string up to 256 characters printable. It MUST begin with a lowercase letter, and
        // can only contain lowercase letters a-z, digits 0-9, underscores _, dashes -, asterisks *,
        // forward slashes / and @

        var i = 0;

        if (key.IsEmpty
            || key.Length > KeyMaxSize
            || ((!(key[i] >= 'a' && key[i] <= 'z')) && (!(key[i] >= '0' && key[i] <= '9'))))
        {
            return false;
        }

        // before
        for (i = 1; i < key.Length; i++)
        {
            var c = key[i];

            if (c == '@')
            {
                // vendor follows
                break;
            }

            if (!(c >= 'a' && c <= 'z')
                && !(c >= '0' && c <= '9')
                && c != '_'
                && c != '-'
                && c != '*'
                && c != '/')
            {
                return false;
            }
        }

        i++; // skip @ or increment further than key.Length

        var vendorLength = key.Length - i;
        if (vendorLength == 0 || vendorLength > 14)
        {
            // vendor name should be at least 1 to 14 character long
            return false;
        }

        if (vendorLength > 0 && i > 242)
        {
            // tenant section should be less than 241 characters long
            return false;
        }

        for (; i < key.Length; i++)
        {
            var c = key[i];

            if (!(c >= 'a' && c <= 'z')
                && !(c >= '0' && c <= '9')
                && c != '_'
                && c != '-'
                && c != '*'
                && c != '/')
            {
                return false;
            }
        }

        return true;
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
            if (c == ',' || c == '=' || c < ' ' /* '\u0020' */ || c > '~' /* '\u007E' */)
            {
                return false;
            }
        }

        return true;
    }
}
