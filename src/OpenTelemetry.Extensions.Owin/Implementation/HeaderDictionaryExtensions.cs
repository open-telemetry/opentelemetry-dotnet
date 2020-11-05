// <copyright file="HeaderDictionaryExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using System.Diagnostics;
using System.Linq;
using Microsoft.Owin;

namespace OpenTelemetry.Implementation
{
    internal static class HeaderDictionaryExtensions
    {
        private const int CorrelationContextHeaderMaxLength = 8192;
        private const int CorrelationContextMaxPairs = 180;

        /// <summary>
        /// Reads Correlation-Context and populates it on Activity.Baggage following https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HttpCorrelationProtocol.md#correlation-context.
        /// Use this method when you want force parsing Correlation-Context is absence of Request-Id or traceparent.
        /// </summary>
        /// <param name="headers">Header collection.</param>
        /// <param name="activity">Activity to populate baggage on.</param>
        // Based on the code from https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.15.0/WEB/Src/Common/WebHeaderCollectionExtensions.cs#L135.
        public static void ReadActivityBaggage(this IHeaderDictionary headers, Activity activity)
        {
            Debug.Assert(headers != null, "Headers must not be null");
            Debug.Assert(activity != null, "Activity must not be null");
            Debug.Assert(!activity.Baggage.Any(), "Baggage must be empty");

            int itemsCount = 0;
            var correlationContexts = headers.GetValues(HeaderNames.CorrelationContext);
            if (correlationContexts == null || correlationContexts.Count == 0)
            {
                return;
            }

            int overallLength = 0;
            foreach (var cc in correlationContexts)
            {
                var headerValue = cc.AsSpan();
                int currentLength = 0;
                int initialLength = headerValue.Length;
                while (itemsCount < CorrelationContextMaxPairs && currentLength < initialLength)
                {
                    var nextSegment = headerValue.Slice(currentLength);
                    var nextComma = nextSegment.IndexOf(',');
                    if (nextComma < 0)
                    {
                        // last one
                        nextComma = nextSegment.Length;
                    }

                    if (nextComma == 0)
                    {
                        currentLength += 1;
                        overallLength += 1;
                        continue;
                    }

                    if (overallLength + nextComma >= CorrelationContextHeaderMaxLength)
                    {
                        return;
                    }

                    ReadOnlySpan<char> kvp = nextSegment.Slice(0, nextComma).Trim();

                    var separatorInd = kvp.IndexOf('=');
                    if (separatorInd > 0 && separatorInd < kvp.Length - 1)
                    {
                        var separatorIndNext = kvp.Slice(separatorInd + 1).IndexOf('=');

                        // check there is just one '=' in key-value-pair
                        if (separatorIndNext < 0)
                        {
                            var baggageKey = kvp.Slice(0, separatorInd).Trim().ToString();
                            var baggageValue = kvp.Slice(separatorInd + 1).Trim().ToString();
                            activity.AddBaggage(baggageKey, baggageValue);
                            itemsCount += 1;
                        }
                    }

                    currentLength += nextComma + 1;
                    overallLength += nextComma + 1;
                }
            }
        }
    }
}
