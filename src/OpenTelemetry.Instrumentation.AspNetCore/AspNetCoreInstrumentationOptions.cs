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
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AspNetCore
{
    /// <summary>
    /// Options for requests instrumentation.
    /// </summary>
    public class AspNetCoreInstrumentationOptions
    {
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
        /// The functions gets the HttpContext, and should return a boolean.
        /// If functions returns true, the request is collected.
        /// If functions returns false, the request is filtered out.
        /// If filter throws exception, then this is considered as no filter being configured.
        /// and requested is collected.
        /// </summary>
        public Func<HttpContext, bool> Filter { get; set; }
    }
}
