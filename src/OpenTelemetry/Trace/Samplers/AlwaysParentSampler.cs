// <copyright file="AlwaysParentSampler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Samplers
{
    /// <summary>
    /// Sampler implementation which will sample in, if parent span is sampled in.
    /// If parent span is invalid, span is sampled out.
    /// </summary>
    public sealed class AlwaysParentSampler : Sampler
    {
        /// <inheritdoc />
        public override string Description { get; } = nameof(AlwaysParentSampler);

        /// <inheritdoc />
        public override SamplingResult ShouldSample(
            in SpanContext parentContext,
            in ActivityTraceId traceId,
            string name,
            SpanKind spanKind,
            IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<Link> parentLinks)
        {
            if (parentContext.IsValid && parentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded))
            {
                return new SamplingResult(true);
            }

            return new SamplingResult(false);
        }
    }
}
