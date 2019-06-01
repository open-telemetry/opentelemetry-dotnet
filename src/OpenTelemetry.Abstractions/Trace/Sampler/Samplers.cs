// <copyright file="Samplers.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Sampler
{
    /// <summary>
    /// Factory of well-known samplers.
    /// </summary>
    public sealed class Samplers
    {
        /// <summary>
        /// Gets the sampler that always sample.
        /// </summary>
        public static ISampler AlwaysSample { get; } = new AlwaysSampleSampler();

        /// <summary>
        /// Gets the sampler than never samples.
        /// </summary>
        public static ISampler NeverSample { get; } = new NeverSampleSampler();

        /// <summary>
        /// Gets the probability sampler.
        /// </summary>
        /// <param name="probability">Probability to use.</param>
        /// <returns>Sampler that samples with the given probability.</returns>
        public static ISampler GetProbabilitySampler(double probability)
        {
            return ProbabilitySampler.Create(probability);
        }
    }
}
