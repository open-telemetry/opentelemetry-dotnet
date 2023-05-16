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

public sealed class ExponentialHistogramData
{
    internal ExponentialHistogramData()
    {
        this.PositiveBuckets = new();
        this.NegativeBuckets = new();
    }

    public int Scale { get; internal set; }

    public long ZeroCount { get; internal set; }

    public ExponentialHistogramBuckets PositiveBuckets { get; private set; }

    internal ExponentialHistogramBuckets NegativeBuckets { get; private set; }

    internal ExponentialHistogramData Copy()
    {
        var copy = new ExponentialHistogramData();
        copy.Scale = this.Scale;
        copy.ZeroCount = this.ZeroCount;
        copy.PositiveBuckets = this.PositiveBuckets.Copy();
        copy.NegativeBuckets = this.NegativeBuckets.Copy();
        return copy;
    }
}
