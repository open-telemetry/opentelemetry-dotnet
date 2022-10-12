// <copyright file="MyCustomDistributedContextPropagator.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Net;

namespace Examples.AspNetCore
{
    public class MyCustomDistributedContextPropagator : DistributedContextPropagator
    {
        private const string TraceParent = "traceparent";
        private const string TraceState = "tracestate";
        private const string BaggageField = "baggage";
        private const char Space = ' ';
        private const char Tab = (char)9;
        private const char Comma = ',';
        private const char Semicolon = ';';
        private const string CommaWithSpace = ", ";
        private const string CorrelationContext = "Correlation-Context";

        private static readonly char[] STrimmingSpaceCharacters = new char[] { Space, Tab };

        public override IReadOnlyCollection<string> Fields => new HashSet<string> { TraceState, TraceParent, BaggageField };

        public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
        {
            if (getter is null)
            {
                traceId = null;
                traceState = null;
                return;
            }

            getter(carrier, TraceParent, out traceId, out var fieldValues);

            getter(carrier, TraceState, out traceState, out _);
        }

        public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter)
        {
            if (activity is null || setter is null)
            {
                return;
            }

            string? id = activity.Id;
            if (id is null)
            {
                return;
            }

            if (activity.IdFormat == ActivityIdFormat.W3C)
            {
                setter(carrier, TraceParent, id);
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    setter(carrier, TraceState, activity.TraceStateString);
                }
            }
        }

        public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter)
        {
            if (getter is null)
            {
                return null;
            }

            getter(carrier, BaggageField, out string? theBaggage, out var baggagevalues);

            IEnumerable<KeyValuePair<string, string?>>? baggage = null;
            if (theBaggage is null || !TryExtractBaggage(theBaggage, out baggage))
            {
                getter(carrier, CorrelationContext, out theBaggage, out _);
                if (theBaggage is not null)
                {
                    TryExtractBaggage(theBaggage, out baggage);
                }
            }

            return baggage;
        }

        internal static bool TryExtractBaggage(string baggageString, out IEnumerable<KeyValuePair<string, string?>>? baggage)
        {
            baggage = null;
            List<KeyValuePair<string, string?>>? baggageList = null;

            if (string.IsNullOrEmpty(baggageString))
            {
                return true;
            }

            int currentIndex = 0;

            do
            {
                // Skip spaces
                while (currentIndex < baggageString.Length && (baggageString[currentIndex] == Space || baggageString[currentIndex] == Tab))
                {
                    currentIndex++;
                }

                if (currentIndex >= baggageString.Length)
                {
                    break; // No Key exist
                }

                int keyStart = currentIndex;

                // Search end of the key
                while (currentIndex < baggageString.Length && baggageString[currentIndex] != Space && baggageString[currentIndex] != Tab && baggageString[currentIndex] != '=')
                {
                    currentIndex++;
                }

                if (currentIndex >= baggageString.Length)
                {
                    break;
                }

                int keyEnd = currentIndex;

                if (baggageString[currentIndex] != '=')
                {
                    // Skip Spaces
                    while (currentIndex < baggageString.Length && (baggageString[currentIndex] == Space || baggageString[currentIndex] == Tab))
                    {
                        currentIndex++;
                    }

                    if (currentIndex >= baggageString.Length)
                    {
                        break; // Wrong key format
                    }

                    if (baggageString[currentIndex] != '=')
                    {
                        break; // wrong key format.
                    }
                }

                currentIndex++;

                // Skip spaces
                while (currentIndex < baggageString.Length && (baggageString[currentIndex] == Space || baggageString[currentIndex] == Tab))
                {
                    currentIndex++;
                }

                if (currentIndex >= baggageString.Length)
                {
                    break; // Wrong value format
                }

                int valueStart = currentIndex;

                // Search end of the value
                while (currentIndex < baggageString.Length && baggageString[currentIndex] != Space && baggageString[currentIndex] != Tab &&
                       baggageString[currentIndex] != Comma && baggageString[currentIndex] != Semicolon)
                {
                    currentIndex++;
                }

                if (keyStart < keyEnd && valueStart < currentIndex)
                {
                    baggageList ??= new List<KeyValuePair<string, string?>>();

                    // Insert in reverse order for asp.net compatibility.
                    baggageList.Insert(0, new KeyValuePair<string, string?>(
                                                WebUtility.UrlDecode(baggageString.Substring(keyStart, keyEnd - keyStart)).Trim(STrimmingSpaceCharacters),
                                                WebUtility.UrlDecode(baggageString.Substring(valueStart, currentIndex - valueStart)).Trim(STrimmingSpaceCharacters)));
                }

                // Skip to end of values
                while (currentIndex < baggageString.Length && baggageString[currentIndex] != Comma)
                {
                    currentIndex++;
                }

                currentIndex++; // Move to next key-value entry
            }
            while (currentIndex < baggageString.Length);

            baggage = baggageList;
            return baggageList != null;
        }
    }
}
