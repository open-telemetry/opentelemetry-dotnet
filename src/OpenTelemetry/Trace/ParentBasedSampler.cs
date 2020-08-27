﻿// <copyright file="ParentBasedSampler.cs" company="OpenTelemetry Authors">
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
    /// Sampler implementation which will take a sample if parent Activity or any linked Activity is sampled.
    /// Otherwise, samples root traces according to the specified delegate sampler.
    /// </summary>
    public sealed class ParentBasedSampler : Sampler
    {
        private readonly Sampler delegateSampler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParentBasedSampler"/> class.
        /// </summary>
        /// <param name="delegateSampler">The <see cref="Sampler"/> to be called to decide whether or not to sample a root trace.</param>
        public ParentBasedSampler(Sampler delegateSampler)
        {
            this.delegateSampler = delegateSampler ?? throw new ArgumentNullException(nameof(delegateSampler));

            this.Description = $"ParentBased{{{delegateSampler.Description}}}";
        }

        /// <inheritdoc />
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            var parentContext = samplingParameters.ParentContext;
            if (/* TODO: TraceId is always provided due to AutoGenerateRootContextTraceId. That is being removed in RC1 and this can be put back.
                 parentContext.TraceId == default ||*/ parentContext.SpanId == default)
            {
                // If no parent, use the delegate to determine sampling.
                return this.delegateSampler.ShouldSample(samplingParameters);
            }

            // If the parent is sampled keep the sampling decision.
            if ((parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                return new SamplingResult(SamplingDecision.RecordAndSampled);
            }

            if (samplingParameters.Links != null)
            {
                // If any parent link is sampled keep the sampling decision.
                foreach (var parentLink in samplingParameters.Links)
                {
                    if ((parentLink.Context.TraceFlags & ActivityTraceFlags.Recorded) != 0)
                    {
                        return new SamplingResult(SamplingDecision.RecordAndSampled);
                    }
                }
            }

            // If parent was not sampled, do not sample.
            return new SamplingResult(SamplingDecision.NotRecord);
        }
    }
}
