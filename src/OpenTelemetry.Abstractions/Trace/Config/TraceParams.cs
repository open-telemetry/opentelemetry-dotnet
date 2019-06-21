// <copyright file="TraceParams.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Config
{
    using System;
    using OpenTelemetry.Trace.Sampler;

    /// <inheritdoc/>
    public sealed class TraceParams : ITraceParams
    {
        /// <summary>
        /// Default trace parameters.
        /// </summary>
        public static readonly ITraceParams Default =
            new TraceParams(Samplers.AlwaysSample, DefaultSpanMaxNumAttributes, DefaultSpanMaxNumEvents, DefaultSpanMaxNumMessageEvents, DefaultSpanMaxNumLinks);

        private const int DefaultSpanMaxNumAttributes = 32;
        private const int DefaultSpanMaxNumEvents = 32;
        private const int DefaultSpanMaxNumMessageEvents = 128;
        private const int DefaultSpanMaxNumLinks = 128;

        internal TraceParams(ISampler sampler, int maxNumberOfAttributes, int maxNumberOfEvents, int maxNumberOfMessageEvents, int maxNumberOfLinks)
        {
            if (maxNumberOfAttributes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfAttributes));
            }

            if (maxNumberOfEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfEvents));
            }

            if (maxNumberOfMessageEvents <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfMessageEvents));
            }

            if (maxNumberOfLinks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfLinks));
            }

            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.MaxNumberOfAttributes = maxNumberOfAttributes;
            this.MaxNumberOfEvents = maxNumberOfEvents;
            this.MaxNumberOfMessageEvents = maxNumberOfMessageEvents;
            this.MaxNumberOfLinks = maxNumberOfLinks;
        }

        /// <inheritdoc/>
        public ISampler Sampler { get; }

        /// <inheritdoc/>
        public int MaxNumberOfAttributes { get; }

        /// <inheritdoc/>
        public int MaxNumberOfEvents { get; }

        /// <inheritdoc/>
        public int MaxNumberOfMessageEvents { get; }

        /// <inheritdoc/>
        public int MaxNumberOfLinks { get; }

        /// <inheritdoc/>
        public TraceParamsBuilder ToBuilder()
        {
            return new TraceParamsBuilder(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TraceParams{"
                + "sampler=" + this.Sampler + ", "
                + "maxNumberOfAttributes=" + this.MaxNumberOfAttributes + ", "
                + "maxNumberOfAnnotations=" + this.MaxNumberOfEvents + ", "
                + "maxNumberOfMessageEvents=" + this.MaxNumberOfMessageEvents + ", "
                + "maxNumberOfLinks=" + this.MaxNumberOfLinks
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TraceParams that)
            {
                return this.Sampler.Equals(that.Sampler)
                     && (this.MaxNumberOfAttributes == that.MaxNumberOfAttributes)
                     && (this.MaxNumberOfEvents == that.MaxNumberOfEvents)
                     && (this.MaxNumberOfMessageEvents == that.MaxNumberOfMessageEvents)
                     && (this.MaxNumberOfLinks == that.MaxNumberOfLinks);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Sampler.GetHashCode();
            h *= 1000003;
            h ^= this.MaxNumberOfAttributes;
            h *= 1000003;
            h ^= this.MaxNumberOfEvents;
            h *= 1000003;
            h ^= this.MaxNumberOfMessageEvents;
            h *= 1000003;
            h ^= this.MaxNumberOfLinks;
            return h;
        }
    }
}
