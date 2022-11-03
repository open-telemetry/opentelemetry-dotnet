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
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http
{
    /// <summary>
    /// Options for HttpClient instrumentation.
    /// </summary>
    public class HttpClientInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a filter function that determines whether or not to
        /// collect telemetry about requests on a per request basis.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item><b>FilterHttpRequestMessage is only executed on .NET and .NET
        /// Core runtimes. <see cref="HttpClient"/> and <see
        /// cref="HttpWebRequest"/> on .NET and .NET Core are both implemented
        /// using <see cref="HttpRequestMessage"/>.</b></item>
        /// <item>The return value for the filter function is interpreted as:
        /// <list type="bullet">
        /// <item>If filter returns <see langword="true" />, the request is
        /// collected.</item>
        /// <item>If filter returns <see langword="false" /> or throws an
        /// exception the request is NOT collected.</item>
        /// </list></item>
        /// </list>
        /// </remarks>
        public Func<HttpRequestMessage, bool> FilterHttpRequestMessage { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>EnrichWithHttpRequestMessage is only executed on .NET and .NET
        /// Core runtimes. <see cref="HttpClient"/> and <see
        /// cref="HttpWebRequest"/> on .NET and .NET Core are both implemented
        /// using <see cref="HttpRequestMessage"/>.</b></para>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpRequestMessage"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpRequestMessage> EnrichWithHttpRequestMessage { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>EnrichWithHttpResponseMessage is only executed on .NET and .NET
        /// Core runtimes. <see cref="HttpClient"/> and <see
        /// cref="HttpWebRequest"/> on .NET and .NET Core are both implemented
        /// using <see cref="HttpRequestMessage"/>.</b></para>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpResponseMessage"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpResponseMessage> EnrichWithHttpResponseMessage { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="Exception"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>EnrichWithException is called for all runtimes.</b></para>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="Exception"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, Exception> EnrichWithException { get; set; }

        /// <summary>
        /// Gets or sets a filter function that determines whether or not to
        /// collect telemetry about requests on a per request basis.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item><b>FilterHttpWebRequest is only executed on .NET Framework
        /// runtimes. <see cref="HttpClient"/> and <see cref="HttpWebRequest"/>
        /// on .NET Framework are both implemented using <see
        /// cref="HttpWebRequest"/>.</b></item>
        /// <item>The return value for the filter function is interpreted as:
        /// <list type="bullet">
        /// <item>If filter returns <see langword="true" />, the request is
        /// collected.</item>
        /// <item>If filter returns <see langword="false" /> or throws an
        /// exception the request is NOT collected.</item>
        /// </list></item>
        /// </list>
        /// </remarks>
        public Func<HttpWebRequest, bool> FilterHttpWebRequest { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpWebRequest"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>EnrichWithHttpWebRequest is only executed on .NET Framework
        /// runtimes. <see cref="HttpClient"/> and <see cref="HttpWebRequest"/>
        /// on .NET Framework are both implemented using <see
        /// cref="HttpWebRequest"/>.</b></para>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpWebRequest"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpWebRequest> EnrichWithHttpWebRequest { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpWebResponse"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>EnrichWithHttpWebResponse is only executed on .NET Framework
        /// runtimes. <see cref="HttpClient"/> and <see cref="HttpWebRequest"/>
        /// on .NET Framework are both implemented using <see
        /// cref="HttpWebRequest"/>.</b></para>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpWebResponse"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpWebResponse> EnrichWithHttpWebResponse { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether exception will be recorded as an <see cref="ActivityEvent"/> or not.
        /// </summary>
        /// <remarks>
        /// See: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md" />.
        /// </remarks>
        public bool RecordException { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool EventFilterHttpRequestMessage(string activityName, object arg1)
        {
            try
            {
                return
                    this.FilterHttpRequestMessage == null ||
                    !TryParseHttpRequestMessage(activityName, arg1, out HttpRequestMessage requestMessage) ||
                    this.FilterHttpRequestMessage(requestMessage);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                return false;
            }
        }

        internal bool EventFilterHttpWebRequest(HttpWebRequest request)
        {
            try
            {
                return this.FilterHttpWebRequest?.Invoke(request) ?? true;
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
