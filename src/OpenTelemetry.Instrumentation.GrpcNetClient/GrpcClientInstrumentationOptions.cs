// <copyright file="GrpcClientInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.GrpcNetClient
{
    /// <summary>
    /// Options for GrpcClient instrumentation.
    /// </summary>
    public class GrpcClientInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether down stream instrumentation is suppressed (disabled).
        /// </summary>
        public bool SuppressDownstreamInstrumentation { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich the Activity with <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpRequestMessage"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpRequestMessage> EnrichWithHttpRequestMessage { get; set; }

        /// <summary>
        /// Gets or sets an action to enrich an Activity with <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <remarks>
        /// <para><see cref="Activity"/>: the activity being enriched.</para>
        /// <para><see cref="HttpResponseMessage"/> object from which additional information can be extracted to enrich the activity.</para>
        /// </remarks>
        public Action<Activity, HttpResponseMessage> EnrichWithHttpResponseMessage { get; set; }
    }
}
