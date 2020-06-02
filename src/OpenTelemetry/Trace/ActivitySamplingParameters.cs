// <copyright file="ActivitySamplingParameters.cs" company="OpenTelemetry Authors">
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
    /// Sampling parameters passed to an <see cref="ActivitySampler"/> for it to make a sampling decision.
    /// </summary>
    public readonly struct ActivitySamplingParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivitySamplingParameters"/> struct.
        /// </summary>
        /// <param name="parentContext">Parent activity context. Typically taken from the wire.</param>
        /// <param name="traceId">Trace ID of a activity to be created.</param>
        /// <param name="name">The name (DisplayName) of the activity to be created. Note, that the name of the activity is settable.
        /// So this name can be changed later and Sampler implementation should assume that.
        /// Typical example of a name change is when <see cref="Activity"/> representing incoming http request
        /// has a name of url path and then being updated with route name when routing complete.
        /// </param>
        /// <param name="kind">The kind of the Activity to be created.</param>
        /// <param name="tags">Initial set of Tags for the Activity being constructed.</param>
        /// <param name="links">Links associated with the activity.</param>
        public ActivitySamplingParameters(
            ActivityContext parentContext,
            ActivityTraceId traceId,
            string name,
            ActivityKind kind,
            IEnumerable<KeyValuePair<string, string>> tags = null, // TODO: Empty
            IEnumerable<ActivityLink> links = null)
        {
            this.ParentContext = parentContext;
            this.TraceId = traceId;
            this.Name = name;
            this.Kind = kind;
            this.Tags = tags;
            this.Links = links;
        }

        /// <summary>
        /// Gets the parent activity context.
        /// </summary>
        public ActivityContext ParentContext { get; }

        /// <summary>
        /// Gets the trace ID of parent activity or a new generated one for root span/activity.
        /// </summary>
        public ActivityTraceId TraceId { get; }

        /// <summary>
        /// Gets the name to be given to the span/activity.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the kind of span/activity to be created.
        /// </summary>
        public ActivityKind Kind { get; }

        /// <summary>
        /// Gets the tags to be associated to the span/activity to be created.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Tags { get; }

        /// <summary>
        /// Gets the links to be added to the activity to be created.
        /// </summary>
        public IEnumerable<ActivityLink> Links { get; }
    }
}
