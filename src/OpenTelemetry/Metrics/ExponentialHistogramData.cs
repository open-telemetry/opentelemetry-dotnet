// <copyright file="ExponentialHistogramData.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Metrics;

public readonly struct ExponentialHistogramData
{
    internal ExponentialHistogramData(int scale, long zeroCount, CircularBufferBuckets positiveBuckets, CircularBufferBuckets negativeBuckets)
    {
        this.Scale = scale;
        this.ZeroCount = zeroCount;
        this.PositiveBuckets = new(positiveBuckets);
        this.NegativeBuckets = new(negativeBuckets);
    }

    public int Scale { get; }

    public long ZeroCount { get; }

    public ExponentialHistogramBuckets PositiveBuckets { get; }

    public ExponentialHistogramBuckets NegativeBuckets { get; }
}
