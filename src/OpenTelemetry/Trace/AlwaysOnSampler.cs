// <copyright file="AlwaysOnSampler.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Sampler implementation which always returns <c>SamplingDecision.RecordAndSample</c>.
    /// </summary>
    public sealed class AlwaysOnSampler : Sampler
    {
        /// <inheritdoc />
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }
    }
}
