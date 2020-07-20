// <copyright file="HttpWebRequestInstrumentationOptions.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using System.Net;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.Dependencies
{
    /// <summary>
    /// Options for dependencies instrumentation.
    /// </summary>
    public class HttpWebRequestInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether add HTTP version to a trace.
        /// </summary>
        public bool SetHttpFlavor { get; set; } = false;

        /// <summary>
        /// Gets or sets <see cref="ITextFormat"/> for context propagation.
        /// </summary>
        public ITextFormatActivity TextFormat { get; set; } = new TraceContextFormatActivity();

        /// <summary>
        /// Gets or sets an optional callback method for filtering HttpClient requests that are sent through the instrumentation.
        /// </summary>
        public Func<HttpWebRequest, bool> FilterFunc { get; set; }

        internal bool EventFilter(HttpWebRequest request)
        {
            Uri requestUri;
            if (request.Method == "POST"
                && (requestUri = request.RequestUri) != null
                && HttpClientInstrumentationOptions.IsInternalUrl(requestUri))
            {
                return false;
            }

            return this.FilterFunc?.Invoke(request) ?? true;
        }
    }
}
#endif
