// <copyright file="SampledPerSpanNameSummary.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public sealed class SampledPerSpanNameSummary : ISampledPerSpanNameSummary
    {
        internal SampledPerSpanNameSummary(IDictionary<ISampledLatencyBucketBoundaries, int> numbersOfLatencySampledSpans, IDictionary<CanonicalCode, int> numbersOfErrorSampledSpans)
        {
            this.NumbersOfLatencySampledSpans = numbersOfLatencySampledSpans ?? throw new ArgumentNullException(nameof(numbersOfLatencySampledSpans));
            this.NumbersOfErrorSampledSpans = numbersOfErrorSampledSpans ?? throw new ArgumentNullException(nameof(numbersOfErrorSampledSpans));
        }

        public IDictionary<ISampledLatencyBucketBoundaries, int> NumbersOfLatencySampledSpans { get; }

        public IDictionary<CanonicalCode, int> NumbersOfErrorSampledSpans { get; }

        public static ISampledPerSpanNameSummary Create(IDictionary<ISampledLatencyBucketBoundaries, int> numbersOfLatencySampledSpans, IDictionary<CanonicalCode, int> numbersOfErrorSampledSpans)
        {
            if (numbersOfLatencySampledSpans == null)
            {
                throw new ArgumentNullException(nameof(numbersOfLatencySampledSpans));
            }

            if (numbersOfErrorSampledSpans == null)
            {
                throw new ArgumentNullException(nameof(numbersOfErrorSampledSpans));
            }

            IDictionary<ISampledLatencyBucketBoundaries, int> copy1 = new Dictionary<ISampledLatencyBucketBoundaries, int>(numbersOfLatencySampledSpans);
            IDictionary<CanonicalCode, int> copy2 = new Dictionary<CanonicalCode, int>(numbersOfErrorSampledSpans);
            return new SampledPerSpanNameSummary(new ReadOnlyDictionary<ISampledLatencyBucketBoundaries, int>(copy1), new ReadOnlyDictionary<CanonicalCode, int>(copy2));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "SampledPerSpanNameSummary{"
                + "numbersOfLatencySampledSpans=" + this.NumbersOfLatencySampledSpans + ", "
                + "numbersOfErrorSampledSpans=" + this.NumbersOfErrorSampledSpans
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is SampledPerSpanNameSummary that)
            {
                return this.NumbersOfLatencySampledSpans.SequenceEqual(that.NumbersOfLatencySampledSpans)
                     && this.NumbersOfErrorSampledSpans.SequenceEqual(that.NumbersOfErrorSampledSpans);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.NumbersOfLatencySampledSpans.GetHashCode();
            h *= 1000003;
            h ^= this.NumbersOfErrorSampledSpans.GetHashCode();
            return h;
        }
    }
}
