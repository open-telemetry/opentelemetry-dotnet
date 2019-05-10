// <copyright file="SampledSpanStoreLatencyFilter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;

    public sealed class SampledSpanStoreLatencyFilter : ISampledSpanStoreLatencyFilter
    {
        internal SampledSpanStoreLatencyFilter(string spanName, TimeSpan latencyLowerNs, TimeSpan latencyUpperNs, int maxSpansToReturn)
        {
            this.SpanName = spanName ?? throw new ArgumentNullException(nameof(spanName));
            this.LatencyLower = latencyLowerNs;
            this.LatencyUpper = latencyUpperNs;
            this.MaxSpansToReturn = maxSpansToReturn;
        }

        public string SpanName { get; }

        public TimeSpan LatencyLower { get; }

        public TimeSpan LatencyUpper { get; }

        public int MaxSpansToReturn { get; }

        public static ISampledSpanStoreLatencyFilter Create(string spanName, TimeSpan latencyLower, TimeSpan latencyUpper, int maxSpansToReturn)
        {
            if (maxSpansToReturn < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpansToReturn));
            }

            if (latencyLower < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(latencyLower));
            }

            if (latencyUpper < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(latencyUpper));
            }

            return new SampledSpanStoreLatencyFilter(spanName, latencyLower, latencyUpper, maxSpansToReturn);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "LatencyFilter{"
                + "spanName=" + this.SpanName + ", "
                + "latencyLowerNs=" + this.LatencyLower + ", "
                + "latencyUpperNs=" + this.LatencyUpper + ", "
                + "maxSpansToReturn=" + this.MaxSpansToReturn
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is SampledSpanStoreLatencyFilter that)
            {
                return this.SpanName.Equals(that.SpanName)
                     && (this.LatencyLower == that.LatencyLower)
                     && (this.LatencyUpper == that.LatencyUpper)
                     && (this.MaxSpansToReturn == that.MaxSpansToReturn);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= this.SpanName.GetHashCode();
            h *= 1000003;
            h ^= (this.LatencyLower.Ticks >> 32) ^ this.LatencyLower.Ticks;
            h *= 1000003;
            h ^= (this.LatencyUpper.Ticks >> 32) ^ this.LatencyUpper.Ticks;
            h *= 1000003;
            h ^= this.MaxSpansToReturn;
            return (int)h;
        }
    }
}
