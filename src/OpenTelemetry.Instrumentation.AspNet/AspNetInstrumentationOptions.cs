// <copyright file="AspNetInstrumentationOptions.cs" company="OpenTelemetry Authors">
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
using System.Web;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Options for ASP.NET instrumentation.
    /// </summary>
    public class AspNetInstrumentationOptions
    {
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
        /// The Filter gets the HttpContext, and should return a boolean.
        /// If Filter returns true, the request is collected.
        /// If Filter returns false or throw exception, the request is filtered out.
        /// </summary>
        public Func<HttpContext, bool> Filter { get; set; }
    }
}
