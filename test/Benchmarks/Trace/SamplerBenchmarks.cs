// <copyright file="SamplerBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1889 (21H2)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.100-preview.7.22377.5
  [Host]     : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT
  DefaultJob : .NET 6.0.8 (6.0.822.36306), X64 RyuJIT


|       Method        |     Mean |   Error |  StdDev |  Gen 0 | Allocated |
|------------- -------|---------:|--------:|--------:|-------:|----------:|
| TraceIdRatioSampler | 608.1 ns | 4.72 ns | 4.19 ns | 0.0992 |     416 B |

*/

namespace Benchmarks.Trace
{
    public class SamplerBenchmarks
    {
        private readonly ActivitySource source = new("SamplerBenchmarks");

        public SamplerBenchmarks()
        {
            // TODO: Parameterize ratio.
            // TODO: Have sampler which modify tracestate
            Sdk.CreateTracerProviderBuilder()
                .SetSampler(new TraceIdRatioBasedSampler(.5))
                .AddSource(this.source.Name)
                .AddProcessor(new DummyActivityProcessor())
                .Build();
        }

        [Benchmark]
        public void TraceIdRatioSampler()
        {
            using var activity = this.source.StartActivity("Benchmark");
        }

        internal class DummyActivityProcessor : BaseProcessor<Activity>
        {
        }
    }
}
