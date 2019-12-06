﻿// <copyright file="ProbabilitySampler.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Trace.Samplers
{
    /// <inheritdoc />
    public sealed class ProbabilitySampler : Sampler
    {
        private readonly long idUpperBound;
        private readonly double probability;

        public ProbabilitySampler(double probability)
        {
            if (probability < 0.0 || probability > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be in range [0.0, 1.0]");
            }

            this.probability = probability;
            this.Description = $"ProbabilitySampler({this.probability:F6})";

            // Special case the limits, to avoid any possible issues with lack of precision across
            // double/long boundaries. For probability == 0.0, we use Long.MIN_VALUE as this guarantees
            // that we will never sample a trace, even in the case where the id == Long.MIN_VALUE, since
            // Math.Abs(Long.MIN_VALUE) == Long.MIN_VALUE.
            if (this.probability == 0.0)
            {
                this.idUpperBound = long.MinValue;
            }
            else if (this.probability == 1.0)
            {
                this.idUpperBound = long.MaxValue;
            }
            else
            {
                this.idUpperBound = (long)(probability * long.MaxValue);
            }
        }

        /// <inheritdoc />
        public override string Description { get; }

        /// <inheritdoc />
        public override Decision ShouldSample(SpanContext parentContext, ActivityTraceId traceId, ActivitySpanId spanId, string name, IDictionary<string, object> attributes, IEnumerable<Link> links)
        {
            // If the parent is sampled keep the sampling decision.
            if (parentContext != null &&
                parentContext.IsValid &&
                (parentContext.TraceOptions & ActivityTraceFlags.Recorded) != 0)
            {
                return new Decision(true);
            }

            if (links != null)
            {
                // If any parent link is sampled keep the sampling decision.
                foreach (var parentLink in links)
                {
                    if ((parentLink.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0)
                    {
                        return new Decision(true);
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
            Span<byte> traceIdBytes = stackalloc byte[16];
            traceId.CopyTo(traceIdBytes);
            return Math.Abs(this.GetLowerLong(traceIdBytes)) < this.idUpperBound ? new Decision(true) : new Decision(false);
        }

        private long GetLowerLong(ReadOnlySpan<byte> bytes)
        {
            long result = 0;
            for (var i = 0; i < 8; i++)
            {
                result <<= 8;
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                result |= bytes[i] & 0xff;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
            }

            return result;
        }
    }
}
