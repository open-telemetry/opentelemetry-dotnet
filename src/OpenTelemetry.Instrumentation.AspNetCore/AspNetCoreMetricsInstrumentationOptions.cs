// <copyright file="AspNetCoreMetricsInstrumentationOptions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore
{
    /// <summary>
    /// Options for metrics requests instrumentation.
    /// </summary>
    public class AspNetCoreMetricsInstrumentationOptions
    {
        /// <summary>
        /// Delegate for enrichment of recorded metric with additional tags.
        /// </summary>
        /// <param name="context"><see cref="HttpContext"/>: the HttpContext object. Both Request and Response are available.</param>
        /// <param name="tags"><see cref="TagList"/>: List of current tags. You can add additional tags to this list. </param>
        public delegate void AspNetCoreMetricEnrichmentFunc(HttpContext context, ref TagList tags);

        /// <summary>
        /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
        /// The Filter gets the HttpContext, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpContext, bool> Filter { get; set; }

        /// <summary>
        /// Gets or sets an function to enrich a recorded metric with additional custom tags.
        /// </summary>
        public AspNetCoreMetricEnrichmentFunc Enrich { get; set; }
    }
}
