// <copyright file="ISampler.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System.Collections.Generic;

    /// <summary>
    /// Sampler to reduce data volume. This sampler executes before Span object was created.
    /// </summary>
    public interface ISampler
    {
        /// <summary>
        /// Gets the span description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Checks whether span needs to be created and tracked.
        /// </summary>
        /// <param name="parentContext">Parent span context. Typically taken from the wire.</param>
        /// <param name="hasRemoteParent">Indicates whether it was a remote parent.</param>
        /// <param name="traceId">Trace ID of a span to be created.</param>
        /// <param name="spanId">Span ID of a span to be created.</param>
        /// <param name="name"> Name of a span to be created. Note, that the name of the span is settable.
        /// So this name can be changed later and <see cref="ISampler"/> implementation should assume that.
        /// Typical example of a name change is when <see cref="ISpan"/> representing incoming http request
        /// has a name of url path and then being updated with route name when rouing complete.
        /// </param>
        /// <param name="parentLinks">Links associated with the parent span.</param>
        /// <returns>True of span needs to be created. False otherwise.</returns>
        bool ShouldSample(ISpanContext parentContext, bool hasRemoteParent, ITraceId traceId, ISpanId spanId, string name, IEnumerable<ISpan> parentLinks);
    }
}
