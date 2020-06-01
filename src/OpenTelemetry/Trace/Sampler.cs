// <copyright file="Sampler.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Sampler to reduce data volume. This sampler executes before Span object was created.
    /// </summary>
    public abstract class Sampler
    {
        /// <summary>
        /// Gets the sampler description.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Checks whether span needs to be created and tracked.
        /// </summary>
        /// <param name="parentContext">Parent span context. Typically taken from the wire.</param>
        /// <param name="traceId">Trace ID of a span to be created.</param>
        /// <param name="name"> Name of a span to be created. Note, that the name of the span is settable.
        ///     So this name can be changed later and Sampler implementation should assume that.
        ///     Typical example of a name change is when <see cref="TelemetrySpan"/> representing incoming http request
        ///     has a name of url path and then being updated with route name when routing complete.
        /// </param>
        /// <param name="spanKind">The type of the Span.</param>
        /// <param name="attributes">Initial set of Attributes for the Span being constructed.</param>
        /// <param name="links">Links associated with the span.</param>
        /// <returns>Sampling decision on whether Span needs to be sampled or not.</returns>
        public abstract SamplingResult ShouldSample(
            in SpanContext parentContext,
            in ActivityTraceId traceId,
            string name,
            SpanKind spanKind,
            IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<Link> links);
    }
}
