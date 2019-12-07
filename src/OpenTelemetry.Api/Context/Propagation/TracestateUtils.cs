﻿// <copyright file="TracestateUtils.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

#if API
namespace OpenTelemetry.Api.Context.Propagation
#else
namespace OpenTelemetry.Context.Propagation
#endif
{
    /// <summary>
    /// Extension methods to extract Tracestate from string.
    /// </summary>
    internal static class TracestateUtils
    {
        private const int KeyMaxSize = 256;
        private const int ValueMaxSize = 256;
        private const int MaxKeyValuePairsCount = 32;

        /// <summary>
        /// Extracts tracestate pairs from the given string and appends it to provided tracestate list"/>"/>.
        /// </summary>
        /// <param name="tracestateString">String with comma separated tracestate key value pairs.</param>
        /// <param name="tracestate"><see cref="List{T}"/> to set tracestate pairs on.</param>
        /// <returns>True if string was parsed successfully and tracestate was recognized, false otherwise.</returns>
        internal static bool AppendTracestate(string tracestateString, List<KeyValuePair<string, string>> tracestate)
        {
            if (string.IsNullOrEmpty(tracestateString))
            {
                return false;
            }

            try
            {
                var names = new HashSet<string>();

                var tracestateSpan = tracestateString.AsSpan().Trim(' ').Trim(',');
                do
                {
                    // tracestate: rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01,congo=BleGNlZWRzIHRohbCBwbGVhc3VyZS4

                    int pairEnd = tracestateSpan.IndexOf(',');
                    if (pairEnd < 0)
                    {
                        pairEnd = tracestateSpan.Length;
                    }

                    if (!TryParseKeyValue(tracestateSpan.Slice(0, pairEnd), out var key, out var value))
                    {
                        return false;
                    }

                    var keyStr = key.ToString();
                    if (names.Add(keyStr))
                    {
                        tracestate.Add(new KeyValuePair<string, string>(keyStr, value.ToString()));
                    }
                    else
                    {
                        return false;
                    }

                    if (tracestate.Count == MaxKeyValuePairsCount)
                    {
                        // TODO: log
                        break;
                    }

                    if (pairEnd == tracestateSpan.Length)
                    {
                        break;
                    }

                    tracestateSpan = tracestateSpan.Slice(pairEnd + 1);
                }
                while (tracestateSpan.Length > 0);

                return true;
            }
            catch (Exception)
            {
                // failure to parse tracestate
                // TODO: logging
            }

            return false;
        }

        internal static string GetString(IEnumerable<KeyValuePair<string, string>> tracestate)
        {
            if (tracestate == null)
            {
                return string.Empty;
            }

            // it's supposedly cheaper to iterate over very short collection a couple of times
            // than to convert it to array.
            var pairsCount = tracestate.Count();
            if (pairsCount == 0)
            {
                return string.Empty;
            }

            if (pairsCount > MaxKeyValuePairsCount)
            {
                // TODO log that tracestate is too big and we'll ignore last entries
            }

            var sb = new StringBuilder();

            int ind = 0;
            foreach (var entry in tracestate)
            {
                if (ind++ < MaxKeyValuePairsCount)
                {
                    // take last MaxKeyValuePairsCount pairs, ignore last (oldest) pairs
                    sb.Append(entry.Key)
                        .Append("=")
                        .Append(entry.Value)
                        .Append(",");
                }
            }

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

            key = pair.Slice(0, keyEndIdx).Trim();
            if (!ValidateKey(key))
            {
                // TODO log
                return false;
            }

            value = pair.Slice(valueStartIdx, pair.Length - valueStartIdx).Trim();
            if (!ValidateValue(value))
            {
                // TODO log
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
                || key[i] < 'a'
                || key[i] > 'z')
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

            if (vendorLength > 0)
            {
                if (i > 242)
                {
                    // tenant section should be less than 241 characters long
                    return false;
                }
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
}
