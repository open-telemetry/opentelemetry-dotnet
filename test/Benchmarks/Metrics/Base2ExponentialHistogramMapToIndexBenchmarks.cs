// <copyright file="Base2ExponentialHistogramMapToIndexBenchmarks.cs" company="OpenTelemetry Authors">
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
BenchmarkDotNet=v0.13.5, OS=macOS Ventura 13.4 (22F66) [Darwin 22.5.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD


|     Method | Scale |     Mean |    Error |   StdDev | Allocated |
|----------- |------ |---------:|---------:|---------:|----------:|
| MapToIndex |   -11 | 11.60 ns | 0.057 ns | 0.053 ns |         - |
| MapToIndex |     3 | 14.63 ns | 0.135 ns | 0.126 ns |         - |
| MapToIndex |    20 | 14.40 ns | 0.026 ns | 0.024 ns |         - |
*/

namespace Benchmarks.Metrics;

public class Base2ExponentialHistogramMapToIndexBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private Base2ExponentialBucketHistogram exponentialHistogram;

    [Params(-11, 3, 20)]
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
