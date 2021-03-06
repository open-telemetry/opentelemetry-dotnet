// <copyright file="ParentBasedSampler.cs" company="OpenTelemetry Authors">
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
using System;
using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Sampler implementation which by default will take a sample if parent Activity is sampled.
    /// Otherwise, samples root traces according to the specified root sampler.
    /// </summary>
    /// <remarks>
    /// The default behavior can be customized by providing additional samplers to be invoked for different
    /// combinations of local/remote parent and its sampling decision.
    /// See <see cref="ParentBasedSampler(Sampler, Sampler, Sampler, Sampler, Sampler)"/>.
    /// </remarks>
    public sealed class ParentBasedSampler : Sampler
    {
        private readonly Sampler rootSampler;

        private readonly Sampler remoteParentSampled;
        private readonly Sampler remoteParentNotSampled;
        private readonly Sampler localParentSampled;
        private readonly Sampler localParentNotSampled;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParentBasedSampler"/> class.
        /// </summary>
        /// <param name="rootSampler">The <see cref="Sampler"/> to be called for root span/activity.</param>
        public ParentBasedSampler(Sampler rootSampler)
        {
            this.rootSampler = rootSampler ?? throw new ArgumentNullException(nameof(rootSampler));

            this.Description = $"ParentBased{{{rootSampler.Description}}}";

            this.remoteParentSampled = new AlwaysOnSampler();
            this.remoteParentNotSampled = new AlwaysOffSampler();
            this.localParentSampled = new AlwaysOnSampler();
            this.localParentNotSampled = new AlwaysOffSampler();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParentBasedSampler"/> class with ability to delegate
        /// sampling decision to one of the inner samplers provided.
        /// </summary>
        /// <param name="rootSampler">The <see cref="Sampler"/> to be called for root span/activity.</param>
        /// <param name="remoteParentSampled">
        /// A <see cref="Sampler"/> to delegate sampling decision to in case of
        /// remote parent (<see cref="ActivityContext.IsRemote"/> == true) with <see cref="ActivityTraceFlags.Recorded"/> flag == true.
        /// Default: <see cref="AlwaysOnSampler"/>.
        /// </param>
        /// <param name="remoteParentNotSampled">
        /// A <see cref="Sampler"/> to delegate sampling decision to in case of
        /// remote parent (<see cref="ActivityContext.IsRemote"/> == true) with <see cref="ActivityTraceFlags.Recorded"/> flag == false.
        /// Default: <see cref="AlwaysOffSampler"/>.
        /// </param>
        /// <param name="localParentSampled">
        /// A <see cref="Sampler"/> to delegate sampling decision to in case of
        /// local parent (<see cref="ActivityContext.IsRemote"/> == false) with <see cref="ActivityTraceFlags.Recorded"/> flag == true.
        /// Default: <see cref="AlwaysOnSampler"/>.
        /// </param>
        /// <param name="localParentNotSampled">
        /// A <see cref="Sampler"/> to delegate sampling decision to in case of
        /// local parent (<see cref="ActivityContext.IsRemote"/> == false) with <see cref="ActivityTraceFlags.Recorded"/> flag == false.
        /// Default: <see cref="AlwaysOffSampler"/>.
        /// </param>
        public ParentBasedSampler(
            Sampler rootSampler,
            Sampler remoteParentSampled = null,
            Sampler remoteParentNotSampled = null,
            Sampler localParentSampled = null,
            Sampler localParentNotSampled = null)
            : this(rootSampler)
        {
            this.remoteParentSampled = remoteParentSampled ?? new AlwaysOnSampler();
            this.remoteParentNotSampled = remoteParentNotSampled ?? new AlwaysOffSampler();
            this.localParentSampled = localParentSampled ?? new AlwaysOnSampler();
            this.localParentNotSampled = localParentNotSampled ?? new AlwaysOffSampler();
        }

        /// <inheritdoc />
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            var parentContext = samplingParameters.ParentContext;
            if (parentContext.TraceId == default)
            {
                // If no parent, use the rootSampler to determine sampling.
                return this.rootSampler.ShouldSample(samplingParameters);
            }

            // Is parent sampled?
            if ((parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                if (parentContext.IsRemote)
                {
                    return this.remoteParentSampled.ShouldSample(samplingParameters);
                }
                else
                {
                    return this.localParentSampled.ShouldSample(samplingParameters);
                }
            }

            // If parent is not sampled => delegate to the "not sampled" inner samplers.
            if (parentContext.IsRemote)
            {
                return this.remoteParentNotSampled.ShouldSample(samplingParameters);
            }
            else
            {
                return this.localParentNotSampled.ShouldSample(samplingParameters);
            }
        }
    }
}
