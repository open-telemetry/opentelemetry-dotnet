// <copyright file="LatencyBucketBoundaries.cs" company="OpenCensus Authors">
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

    public class LatencyBucketBoundaries : ISampledLatencyBucketBoundaries
    {
        public static readonly ISampledLatencyBucketBoundaries ZeroMicrosx10 = new LatencyBucketBoundaries(TimeSpan.Zero, TimeSpan.FromTicks(100));
        public static readonly ISampledLatencyBucketBoundaries Microsx10Microsx100 = new LatencyBucketBoundaries(TimeSpan.FromTicks(100), TimeSpan.FromTicks(1000));
        public static readonly ISampledLatencyBucketBoundaries Microsx100Millix1 = new LatencyBucketBoundaries(TimeSpan.FromTicks(1000), TimeSpan.FromMilliseconds(1));
        public static readonly ISampledLatencyBucketBoundaries Millix1Millix10 = new LatencyBucketBoundaries(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(10));
        public static readonly ISampledLatencyBucketBoundaries Millix10Millix100 = new LatencyBucketBoundaries(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100));
        public static readonly ISampledLatencyBucketBoundaries Millix100Secondx1 = new LatencyBucketBoundaries(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        public static readonly ISampledLatencyBucketBoundaries Secondx1Secondx10 = new LatencyBucketBoundaries(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
        public static readonly ISampledLatencyBucketBoundaries Secondx10Secondx100 = new LatencyBucketBoundaries(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(100));
        public static readonly ISampledLatencyBucketBoundaries Secondx100Max = new LatencyBucketBoundaries(TimeSpan.FromSeconds(100), TimeSpan.MaxValue);

        public static IReadOnlyList<ISampledLatencyBucketBoundaries> Values = new List<ISampledLatencyBucketBoundaries>
        {
            ZeroMicrosx10, Microsx10Microsx100, Microsx100Millix1, Millix1Millix10, Millix10Millix100, Millix100Secondx1, Secondx1Secondx10, Secondx10Secondx100, Secondx100Max,
        };

        internal LatencyBucketBoundaries(TimeSpan latencyLowerNs, TimeSpan latencyUpperNs)
        {
            this.LatencyLower = latencyLowerNs;
            this.LatencyUpper = latencyUpperNs;
        }

        public TimeSpan LatencyLower { get; }

        public TimeSpan LatencyUpper { get; }
    }
}
