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
        /// Gets or sets <see cref="ITextFormat"/> for context propagation. Default value: <see cref="CompositePropagator"/> with <see cref="TraceContextFormat"/> &amp; <see cref="BaggageFormat"/>.
        /// </summary>
        public ITextFormat TextFormat { get; set; } = new CompositePropagator(new ITextFormat[]
        {
            new TraceContextFormat(),
            new BaggageFormat(),
        });

        /// <summary>
        /// Gets or sets a Filter function to filter instrumentation for requests on a per request basis.
        /// The functions gets the HttpRequestMessage, and should return a boolean indicating if the request
        /// should be filtered or not.
        /// If functions returns true, the request is filtered.
        /// If functions returns false or throws exceptions, the request is collected. This is also the
        /// default behavior is no filter is configured.
        /// </summary>
        public Func<HttpRequestMessage, bool> InstrumentationFilter { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool EventFilter(string activityName, object arg1)
        {
            try
            {
                return
                    this.InstrumentationFilter == null ||
                    !TryParseHttpRequestMessage(activityName, arg1, out HttpRequestMessage requestMessage) ||
                    !this.InstrumentationFilter(requestMessage);
            }
            catch (Exception ex)
            {
                HttpInstrumentationEventSource.Log.RequestFilterException(ex);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseHttpRequestMessage(string activityName, object arg1, out HttpRequestMessage requestMessage)
        {
            return (requestMessage = arg1 as HttpRequestMessage) != null && activityName == "System.Net.Http.HttpRequestOut";
        }
    }
}
