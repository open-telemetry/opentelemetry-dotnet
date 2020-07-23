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
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Sampler to select data to be exported. This sampler executes before Activity object is created.
    /// </summary>
    public abstract class Sampler
    {
        /// <summary>
        /// Gets the sampler description.
        /// </summary>
        public abstract string Description
        {
            get
            {
                this.description = this.description ?? this.GetType().Name;
                return this.description;
            }
            set { this.description = value; }
        }

        /// <summary>
        /// Checks whether activity needs to be created and tracked.
        /// </summary>
        /// <param name="samplingParameters">
        /// The <see cref="SamplingParameters"/> used by the <see cref="Sampler"/>
        /// to decide if the <see cref="Activity"/> to be created is going to be sampled or not.
        /// </param>
        /// <returns>Sampling decision on whether activity needs to be sampled or not.</returns>
        public abstract SamplingResult ShouldSample(in SamplingParameters samplingParameters);

        private string description;
    }
}
