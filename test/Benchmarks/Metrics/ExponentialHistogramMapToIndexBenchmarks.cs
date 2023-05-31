// <copyright file="ExponentialHistogramMapToIndexBenchmarks.cs" company="OpenTelemetry Authors">
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

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.13.3, OS=macOS 13.2.1 (22D68) [Darwin 22.3.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD


|           Method | Scale |     Mean |    Error |   StdDev | Allocated |
|----------------- |------ |---------:|---------:|---------:|----------:|
|       MapToIndex |   -11 | 11.59 ns | 0.069 ns | 0.065 ns |         - |
|       MapToIndex |    20 | 14.50 ns | 0.037 ns | 0.033 ns |         - |
*/

namespace Benchmarks.Metrics
{
    public class ExponentialHistogramMapToIndexBenchmarks
    {
        private const int MaxValue = 10000;
        private readonly Random random = new();
        private Base2ExponentialBucketHistogram exponentialHistogram;

        [Params(-11, 20)]
        public int Scale { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.exponentialHistogram = new Base2ExponentialBucketHistogram(scale: this.Scale);
        }

        [Benchmark]
        public void MapToIndex()
        {
            this.exponentialHistogram.MapToIndex(this.random.Next(MaxValue));
        }
    }
}
