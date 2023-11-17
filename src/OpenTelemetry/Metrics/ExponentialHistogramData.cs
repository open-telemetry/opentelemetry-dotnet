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

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains the data for an exponential histogram.
/// </summary>
public sealed class ExponentialHistogramData
{
    internal ExponentialHistogramData()
    {
        this.PositiveBuckets = new();
        this.NegativeBuckets = new();
    }

    /// <summary>
    /// Gets the exponential histogram scale.
    /// </summary>
    public int Scale { get; internal set; }

    /// <summary>
    /// Gets the exponential histogram zero count.
    /// </summary>
    public long ZeroCount { get; internal set; }

    /// <summary>
    /// Gets the exponential histogram positive buckets.
    /// </summary>
    public ExponentialHistogramBuckets PositiveBuckets { get; private set; }

    /// <summary>
    /// Gets the exponential histogram negative buckets.
    /// </summary>
    internal ExponentialHistogramBuckets NegativeBuckets { get; private set; }

    internal ExponentialHistogramData Copy()
    {
        var copy = new ExponentialHistogramData
        {
            Scale = this.Scale,
            ZeroCount = this.ZeroCount,
            PositiveBuckets = this.PositiveBuckets.Copy(),
            NegativeBuckets = this.NegativeBuckets.Copy(),
        };
        return copy;
    }
}
