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
BenchmarkDotNet v0.13.10, Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


| Method     | Scale | Mean      | Error     | StdDev    | Allocated |
|----------- |------ |----------:|----------:|----------:|----------:|
| MapToIndex | -11   |  4.003 ns | 0.0288 ns | 0.0240 ns |         - |
| MapToIndex | 3     | 11.081 ns | 0.1222 ns | 0.1143 ns |         - |
| MapToIndex | 20    | 11.077 ns | 0.1103 ns | 0.1032 ns |         - |
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
