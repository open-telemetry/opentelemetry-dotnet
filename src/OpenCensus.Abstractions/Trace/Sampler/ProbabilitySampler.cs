// <copyright file="ProbabilitySampler.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Sampler
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Utils;

    internal sealed class ProbabilitySampler : ISampler
    {
        internal ProbabilitySampler(double probability, long idUpperBound)
        {
            this.Probability = probability;
            this.IdUpperBound = idUpperBound;
        }

        public string Description
        {
            get
            {
                return string.Format("ProbabilitySampler({0:F6})", this.Probability);
            }
        }

        public double Probability { get; }

        public long IdUpperBound { get; }

        public bool ShouldSample(ISpanContext parentContext, bool hasRemoteParent, ITraceId traceId, ISpanId spanId, string name, IEnumerable<ISpan> parentLinks)
        {
            // If the parent is sampled keep the sampling decision.
            if (parentContext != null && parentContext.TraceOptions.IsSampled)
            {
                return true;
            }

            if (parentLinks != null)
            {
                // If any parent link is sampled keep the sampling decision.
                foreach (ISpan parentLink in parentLinks)
                {
                    if (parentLink.Context.TraceOptions.IsSampled)
                    {
                        return true;
                    }
                }
            }

            // Always sample if we are within probability range. This is true even for child spans (that
            // may have had a different sampling decision made) to allow for different sampling policies,
            // and dynamic increases to sampling probabilities for debugging purposes.
            // Note use of '<' for comparison. This ensures that we never sample for probability == 0.0,
            // while allowing for a (very) small chance of *not* sampling if the id == Long.MAX_VALUE.
            // This is considered a reasonable tradeoff for the simplicity/performance requirements (this
            // code is executed in-line for every Span creation).
            return Math.Abs(traceId.LowerLong) < this.IdUpperBound;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "ProbabilitySampler{"
                + "probability=" + this.Probability + ", "
                + "idUpperBound=" + this.IdUpperBound
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is ProbabilitySampler that)
            {
                return DoubleUtil.ToInt64(this.Probability) == DoubleUtil.ToInt64(that.Probability)
                     && (this.IdUpperBound == that.IdUpperBound);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= (DoubleUtil.ToInt64(this.Probability) >> 32) ^ DoubleUtil.ToInt64(this.Probability);
            h *= 1000003;
            h ^= (this.IdUpperBound >> 32) ^ this.IdUpperBound;
            return (int)h;
        }

        internal static ProbabilitySampler Create(double probability)
        {
            if (probability < 0.0 || probability > 1.0)
            {
                throw new ArgumentOutOfRangeException("probability must be in range [0.0, 1.0]");
            }

            long idUpperBound;

            // Special case the limits, to avoid any possible issues with lack of precision across
            // double/long boundaries. For probability == 0.0, we use Long.MIN_VALUE as this guarantees
            // that we will never sample a trace, even in the case where the id == Long.MIN_VALUE, since
            // Math.Abs(Long.MIN_VALUE) == Long.MIN_VALUE.
            if (probability == 0.0)
            {
                idUpperBound = long.MinValue;
            }
            else if (probability == 1.0)
            {
                idUpperBound = long.MaxValue;
            }
            else
            {
                idUpperBound = (long)(probability * long.MaxValue);
            }

            return new ProbabilitySampler(probability, idUpperBound);
        }
    }
}
