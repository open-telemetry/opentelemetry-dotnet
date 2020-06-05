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

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// Options for dependencies instrumentation.
    /// </summary>
    public class HttpClientInstrumentationOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInstrumentationOptions"/> class.
        /// </summary>
        public HttpClientInstrumentationOptions()
        {
            this.EventFilter = DefaultFilter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInstrumentationOptions"/> class.
        /// </summary>
        /// <param name="eventFilter">Custom filtering predicate for DiagnosticSource events, if any.</param>
        internal HttpClientInstrumentationOptions(Func<string, object, object, bool> eventFilter)
        {
            // TODO This API is unusable and likely to change, let's not expose it for now.

            this.EventFilter = eventFilter;
        }

        /// <summary>
        /// Gets or sets a value indicating whether add HTTP version to a trace.
        /// </summary>
        public bool SetHttpFlavor { get; set; } = false;

        /// <summary>
        /// Gets or sets <see cref="ITextFormat"/> for context propagation.
        /// </summary>
        public ITextFormat TextFormat { get; set; } = new TraceContextFormat();

        /// <summary>
        /// Gets a hook to exclude calls based on domain or other per-request criterion.
        /// </summary>
        internal Func<string, object, object, bool> EventFilter { get; }

        private static bool DefaultFilter(string activityName, object arg1, object unused)
        {
            // TODO: there is some preliminary consensus that we should introduce 'terminal' spans or context.
            // exporters should ensure they set it

            if (IsHttpOutgoingPostRequest(activityName, arg1, out Uri requestUri))
            {
                var originalString = requestUri.OriginalString;

                // zipkin
                if (originalString.Contains(":9411/api/v2/spans"))
                {
                    return false;
                }

                // applicationinsights
                if (originalString.StartsWith("https://dc.services.visualstudio") ||
                    originalString.StartsWith("https://rt.services.visualstudio") ||
                    originalString.StartsWith("https://dc.applicationinsights") ||
                    originalString.StartsWith("https://live.applicationinsights") ||
                    originalString.StartsWith("https://quickpulse.applicationinsights"))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHttpOutgoingPostRequest(string activityName, object arg1, out Uri requestUri)
        {
            if (activityName == "System.Net.Http.HttpRequestOut")
            {
                if (arg1 is HttpRequestMessage request &&
                    request.RequestUri != null &&
                    request.Method == HttpMethod.Post)
                {
                    requestUri = request.RequestUri;
                    return true;
                }
            }

            requestUri = null;
            return false;
        }
    }
}
