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
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// Options for HttpClient instrumentation.
    /// </summary>
    public class HttpClientInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not the HTTP version should be added as the <see cref="SemanticConventions.AttributeHttpFlavor"/> tag. Default value: False.
        /// </summary>
        public bool SetHttpFlavor { get; set; }

        /// <summary>
        /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
        /// The Filter gets the HttpRequestMessage, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpRequestMessage, bool> Filter { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para>string: the name of the event.</para>
        /// <para>object: the raw object from which additional information can be extracted to enrich the activity.
        /// The type of this object depends on the event, which is given by the above parameter.</para>
        /// </remarks>
        public Action<Activity, string, object> Enrich { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether exception will be recorded as ActivityEvent or not.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
        /// </remarks>
        public bool RecordException { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool EventFilter(string activityName, object arg1)
        {
            try
            {
                return
                    this.Filter == null ||
                    !TryParseHttpRequestMessage(activityName, arg1, out HttpRequestMessage requestMessage) ||
                    this.Filter(requestMessage);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseHttpRequestMessage(string activityName, object arg1, out HttpRequestMessage requestMessage)
        {
            return (requestMessage = arg1 as HttpRequestMessage) != null && activityName == "System.Net.Http.HttpRequestOut";
        }
    }
}
