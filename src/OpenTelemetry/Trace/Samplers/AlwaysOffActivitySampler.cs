﻿// <copyright file="AlwaysOffActivitySampler.cs" company="OpenTelemetry Authors">
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
    /// Sampler implementation which never samples any activity.
    /// </summary>
    public sealed class AlwaysOffActivitySampler : ActivitySampler
    {
        /// <inheritdoc />
        public override string Description { get; } = nameof(AlwaysOffActivitySampler);

        /// <inheritdoc />
        public override SamplingResult ShouldSample(in ActivityContext parentContext, in ActivityTraceId traceId, in ActivitySpanId spanId, string name, ActivityKind activityKind, IEnumerable<KeyValuePair<string, string>> tags, IEnumerable<ActivityLink> links)
        {
            return new SamplingResult(false);
        }
    }
}
