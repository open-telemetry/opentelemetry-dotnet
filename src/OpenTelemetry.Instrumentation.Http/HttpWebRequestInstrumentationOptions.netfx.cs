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
using System.Diagnostics;
using System.Net;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// Options for HttpWebRequest instrumentation.
    /// </summary>
    public class HttpWebRequestInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
        /// The Filter gets the HttpWebRequest, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpWebRequest, bool> Filter { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpWebRequest"/>.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpWebRequest"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpWebRequest> EnrichWithHttpWebRequest { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpWebResponse"/>.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpWebResponse"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpWebResponse> EnrichWithHttpWebResponse { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="Exception"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, Exception> EnrichWithException { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether exception will be recorded as ActivityEvent or not.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
        /// </remarks>
        public bool RecordException { get; set; }

        internal bool EventFilter(HttpWebRequest request)
        {
            try
            {
                return this.Filter?.Invoke(request) ?? true;
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                return false;
            }
        }
    }
}
#endif
