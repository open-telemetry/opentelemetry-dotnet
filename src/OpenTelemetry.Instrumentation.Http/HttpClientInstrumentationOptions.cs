// <copyright file="HttpClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// Options for dependencies instrumentation.
    /// </summary>
    public class HttpClientInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not the HTTP version should be added as the <see cref="SemanticConventions.AttributeHttpFlavor"/> tag. Default value: False.
        /// </summary>
        public bool SetHttpFlavor { get; set; } = false;

        /// <summary>
        /// Gets or sets <see cref="ITextFormat"/> for context propagation. Default value: <see cref="TraceContextFormat"/>.
        /// </summary>
        public ITextFormat TextFormat { get; set; } = new TraceContextFormat();

        /// <summary>
        /// Gets or sets an optional callback method for filtering <see cref="HttpRequestMessage"/> requests that are sent through the instrumentation.
        /// </summary>
        public Func<HttpRequestMessage, bool> FilterFunc { get; set; }

        internal static bool IsInternalUrl(Uri requestUri)
        {
            var originalString = requestUri.OriginalString;

            // zipkin
            if (originalString.Contains(":9411/api/v2/spans"))
            {
                return true;
            }

            // applicationinsights
            if (originalString.StartsWith("https://dc.services.visualstudio") ||
                originalString.StartsWith("https://rt.services.visualstudio") ||
                originalString.StartsWith("https://dc.applicationinsights") ||
                originalString.StartsWith("https://live.applicationinsights") ||
                originalString.StartsWith("https://quickpulse.applicationinsights"))
            {
                return true;
            }

            return false;
        }

        internal bool EventFilter(string activityName, object arg1)
        {
            if (TryParseHttpRequestMessage(activityName, arg1, out HttpRequestMessage requestMessage))
            {
                Uri requestUri;
                if (requestMessage.Method == HttpMethod.Post && (requestUri = requestMessage.RequestUri) != null)
                {
                    if (IsInternalUrl(requestUri))
                    {
                        return false;
                    }
                }

                return this.FilterFunc?.Invoke(requestMessage) ?? true;
            }

            return true;
        }

        private static bool TryParseHttpRequestMessage(string activityName, object arg1, out HttpRequestMessage requestMessage)
        {
            return (requestMessage = arg1 as HttpRequestMessage) != null && activityName == "System.Net.Http.HttpRequestOut";
        }
    }
}
