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
    /// Options for requests instrumentation.
    /// </summary>
    public class AspNetInstrumentationOptions
    {
        /// <summary>
        /// Gets or sets <see cref="ITextFormat"/> for context propagation. Default value: <see cref="CompositePropagator"/> with <see cref="TextMapPropagator"/> &amp; <see cref="BaggagePropagator"/>.
        /// </summary>
        public ITextFormat TextFormat { get; set; } = new CompositePropagator(new ITextFormat[]
        {
            new TextMapPropagator(),
            new BaggagePropagator(),
        });

        /// <summary>
        /// Gets or sets a hook to exclude calls based on domain or other per-request criterion.
        /// </summary>
        internal Predicate<HttpContext> RequestFilter { get; set; }
    }
}
