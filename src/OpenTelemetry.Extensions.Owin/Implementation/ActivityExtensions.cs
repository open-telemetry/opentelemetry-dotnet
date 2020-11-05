// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Net.Http.Headers;
using Microsoft.Owin;

namespace OpenTelemetry.Implementation
{
    internal static class ActivityExtensions
    {
        /// <summary>
        /// Maximum length of Correlation-Context header value.
        /// </summary>
        private const int MaxCorrelationContextLength = 1024;

        /// <summary>
        /// Reads Request-Id and Correlation-Context headers and sets ParentId and Baggage on Activity.
        /// Based on the code from https://github.com/aspnet/Microsoft.AspNet.TelemetryCorrelation/blob/master/src/Microsoft.AspNet.TelemetryCorrelation/ActivityExtensions.cs#L48
        /// </summary>
        /// <param name="activity">Instance of activity that has not been started yet.</param>
        /// <param name="requestHeaders">Request headers collection.</param>
        /// <returns>true if request was parsed successfully, false - otherwise.</returns>
        public static bool Extract(this Activity activity, IHeaderDictionary requestHeaders)
        {
            if (activity == null)
            {
                OwinExtensionsEventSource.Log.ActivityExtractionError("activity is null");
                return false;
            }

            if (activity.ParentId != null)
            {
                OwinExtensionsEventSource.Log.ActivityExtractionError("ParentId is already set on activity");
                return false;
            }

            if (activity.Id != null)
            {
                OwinExtensionsEventSource.Log.ActivityExtractionError("Activity is already started");
                return false;
            }

            var traceParents = requestHeaders.GetValues(HeaderNames.TraceParent);
            if (traceParents == null || traceParents.Count == 0)
            {
                traceParents = requestHeaders.GetValues(HeaderNames.RequestId);
            }

            if (traceParents != null && traceParents.Count > 0 && !string.IsNullOrEmpty(traceParents[0]))
            {
                // there may be several Request-Id or traceparent headers, but we only read the first one
                activity.SetParentId(traceParents[0]);

                var traceStates = requestHeaders.GetValues(HeaderNames.TraceState);
                if (traceStates != null && traceStates.Count > 0)
                {
                    if (traceStates.Count == 1 && !string.IsNullOrEmpty(traceStates[0]))
                    {
                        activity.TraceStateString = traceStates[0];
                    }
                    else
                    {
                        activity.TraceStateString = string.Join(",", traceStates);
                    }
                }

                // Header format - Correlation-Context: key1=value1, key2=value2
                var baggages = requestHeaders.GetValues(HeaderNames.CorrelationContext);
                if (baggages != null)
                {
                    int correlationContextLength = -1;

                    // there may be several Correlation-Context headers
                    foreach (var item in baggages)
                    {
                        if (correlationContextLength >= MaxCorrelationContextLength)
                        {
                            break;
                        }

                        foreach (var pair in item.Split(','))
                        {
                            correlationContextLength += pair.Length + 1; // pair and comma

                            if (correlationContextLength >= MaxCorrelationContextLength)
                            {
                                break;
                            }

                            if (NameValueHeaderValue.TryParse(pair, out NameValueHeaderValue baggageItem))
                            {
                                activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                            }
                            else
                            {
                                OwinExtensionsEventSource.Log.HeaderParsingError(HeaderNames.CorrelationContext, pair);
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }
    }
}
