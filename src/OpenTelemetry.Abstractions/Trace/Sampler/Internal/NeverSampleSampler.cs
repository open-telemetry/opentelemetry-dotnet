// <copyright file="NeverSampleSampler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Sampler.Internal
{
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <inheritdoc />
    internal sealed class NeverSampleSampler : ISampler
    {
        internal NeverSampleSampler()
        {
        }

        public string Description => this.ToString();

        /// <inheritdoc />
        public bool ShouldSample(SpanContext parentContext, ActivityTraceId traceId, ActivitySpanId spanId, string name, IEnumerable<ILink> links)
        {
            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "NeverSampleSampler";
        }
    }
}
