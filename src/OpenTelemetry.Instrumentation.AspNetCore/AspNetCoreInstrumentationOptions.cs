// <copyright file="AspNetCoreInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore
{
    /// <summary>
    /// Options for requests instrumentation.
    /// </summary>
    public class AspNetCoreInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
        /// The Filter gets the HttpContext, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpContext, bool> Filter { get; set; }

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
        /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md.
        /// </remarks>
        public bool RecordException { get; set; }

#if NETSTANDARD2_1
        /// <summary>
        /// Gets or sets a value indicating whether RPC attributes are added to an Activity when using Grpc.AspNetCore. Default is true.
        /// </summary>
        /// <remarks>
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/rpc.md.
        /// </remarks>
        public bool EnableGrpcAspNetCoreSupport { get; set; } = true;
#endif
    }
}
