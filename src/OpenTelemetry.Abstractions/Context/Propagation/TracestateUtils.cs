// <copyright file="TracestateUtils.cs" company="OpenTelemetry Authors">
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

#if ABSTRACTIONS
namespace OpenTelemetry.Abstractions.Context.Propagation
#else
namespace OpenTelemetry.Context.Propagation
#endif
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Extension methods to extract Tracestate from string.
    /// </summary>
    internal static class TracestateUtils
    {
        /// <summary>
        /// Extracts <see cref="Tracestate"/> from the given string and sets it on provided <see cref="Tracestate.TracestateBuilder"/>.
        /// </summary>
        /// <param name="tracestateString">String with comma separated tracestate key value pairs.</param>
        /// <param name="tracestateBuilder"><see cref="Tracestate.TracestateBuilder"/> to set tracestate pairs on.</param>
        /// <returns>True if string was parsed successfully and tracestate was recognized, false otherwise.</returns>
        internal static bool TryExtractTracestate(string tracestateString, Tracestate.TracestateBuilder tracestateBuilder)
        {
            if (string.IsNullOrWhiteSpace(tracestateString))
            {
                return true;
            }

            try
            {
                var names = new HashSet<string>();

                var tracestate = tracestateString.AsSpan().Trim(' ').Trim(',');
                do
                {
                    // tracestate: rojo=00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01,congo=BleGNlZWRzIHRohbCBwbGVhc3VyZS4

                    // Iterate in reverse order because when call builder set the elements is added in the
                    // front of the list.
                    int pairStart = tracestate.LastIndexOf(',') + 1;

                    if (!TryParseKeyValue(tracestate.Slice(pairStart, tracestate.Length - pairStart), out var key, out var value))
                    {
                        return false;
                    }

                    var keyStr = key.ToString();
                    if (names.Add(keyStr))
                    {
                        tracestateBuilder.Set(keyStr, value.ToString());
                    }
                    else
                    {
                        return false;
                    }

                    if (pairStart == 0)
                    {
                        break;
                    }

                    tracestate = tracestate.Slice(0, pairStart - 1);
                }
                while (tracestate.Length > 0);

                return true;
            }
            catch (Exception)
            {
                // failure to parse tracestate
                // TODO: logging
            }

            return false;
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
            value = pair.Slice(valueStartIdx, pair.Length - valueStartIdx).Trim();
            return true;
        }
    }
}
