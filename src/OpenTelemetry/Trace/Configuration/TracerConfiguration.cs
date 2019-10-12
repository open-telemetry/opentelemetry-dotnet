// <copyright file="TracerConfiguration.cs" company="OpenTelemetry Authors">
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
using System;
using OpenTelemetry.Trace.Sampler;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Trace configuration that can be updates in runtime.
    /// </summary>
    public sealed class TracerConfiguration
    {
        private const int DefaultSpanMaxNumAttributes = 32;
        private const int DefaultSpanMaxNumEvents = 128;
        private const int DefaultSpanMaxNumLinks = 32;

        public TracerConfiguration()
            : this(Samplers.AlwaysSample, DefaultSpanMaxNumAttributes, DefaultSpanMaxNumEvents, DefaultSpanMaxNumLinks)
        {
        }

        public TracerConfiguration(ISampler sampler)
            : this(sampler, DefaultSpanMaxNumAttributes, DefaultSpanMaxNumEvents, DefaultSpanMaxNumLinks)
        {
        }

        public TracerConfiguration(ISampler sampler, int maxNumberOfAttributes, int maxNumberOfEvents, int maxNumberOfLinks)
        {
            if (maxNumberOfAttributes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfAttributes));
            }

            if (maxNumberOfEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfEvents));
            }

            if (maxNumberOfLinks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfLinks));
            }

            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.MaxNumberOfAttributes = maxNumberOfAttributes;
            this.MaxNumberOfEvents = maxNumberOfEvents;
            this.MaxNumberOfLinks = maxNumberOfLinks;
        }

        /// <summary>
        /// Gets the sampler.
        /// </summary>
        public ISampler Sampler { get; }

        /// <summary>
        /// Gets the maximum number of attributes on span.
        /// </summary>
        public int MaxNumberOfAttributes { get; }

        /// <summary>
        /// Gets the maximum number of events on span.
        /// </summary>
        public int MaxNumberOfEvents { get; }

        /// <summary>
        /// Gets the maximum number of links on span.
        /// </summary>
        public int MaxNumberOfLinks { get; }
    }
}
