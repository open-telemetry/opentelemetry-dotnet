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
using System.Runtime.CompilerServices;
using OpenTelemetry.Context.Propagation;
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
        /// Gets or sets <see cref="IPropagator"/> for context propagation. Default value: <see cref="CompositePropagator"/> with <see cref="TextMapPropagator"/> &amp; <see cref="BaggagePropagator"/>.
        /// </summary>
        public IPropagator Propagator { get; set; } = new CompositePropagator(new IPropagator[]
        {
            new TextMapPropagator(),
            new BaggagePropagator(),
        });

        /// <summary>
        /// Gets or sets a Filter function to filter instrumentation for requests on a per request basis.
        /// The Filter gets the HttpRequestMessage, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpRequestMessage, bool> Filter { get; set; }

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
