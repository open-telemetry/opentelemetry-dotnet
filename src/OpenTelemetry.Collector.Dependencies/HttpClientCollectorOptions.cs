// <copyright file="HttpClientCollectorOptions.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Collector.Dependencies
{
    /// <summary>
    /// Options for dependencies collector.
    /// </summary>
    public class HttpClientCollectorOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientCollectorOptions"/> class.
        /// </summary>
        public HttpClientCollectorOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientCollectorOptions"/> class.
        /// </summary>
        /// <param name="eventFilter">Custom filtering predicate for DiagnosticSource events, if any.</param>
        internal HttpClientCollectorOptions(Func<string, object, object, bool> eventFilter)
        {
            // TODO This API is unusable and likely to change, let's not expose it for now.

            this.EventFilter = eventFilter;
        }

        /// <summary>
        /// Gets or sets a value indicating whether add HTTP version to a trace.
        /// </summary>
        public bool SetHttpFlavor { get; set; } = false;

        /// <summary>
        /// Gets or sets <see cref="ITextFormat"/> for context propagation.
        /// </summary>
        public ITextFormat TextFormat { get; set; } = new TraceContextFormat();

        /// <summary>
        /// Gets a hook to exclude calls based on domain or other per-request criterion.
        /// </summary>
        internal Func<string, object, object, bool> EventFilter { get; }
    }
}
