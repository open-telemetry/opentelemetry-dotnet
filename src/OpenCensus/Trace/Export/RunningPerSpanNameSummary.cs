// <copyright file="RunningPerSpanNameSummary.cs" company="OpenCensus Authors">
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

    public sealed class RunningPerSpanNameSummary : IRunningPerSpanNameSummary
    {
        internal RunningPerSpanNameSummary(int numRunningSpans)
        {
            this.NumRunningSpans = numRunningSpans;
        }

        public int NumRunningSpans { get; }

        public static IRunningPerSpanNameSummary Create(int numRunningSpans)
        {
            if (numRunningSpans < 0)
            {
                throw new ArgumentOutOfRangeException("Negative numRunningSpans.");
            }

            return new RunningPerSpanNameSummary(numRunningSpans);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "RunningPerSpanNameSummary{"
                + "numRunningSpans=" + this.NumRunningSpans
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is RunningPerSpanNameSummary that)
            {
                return this.NumRunningSpans == that.NumRunningSpans;
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.NumRunningSpans;
            return h;
        }
    }
}
