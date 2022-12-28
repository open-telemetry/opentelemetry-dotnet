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


|                        Method |     Mean |   Error |  StdDev |  Gen 0 | Allocated |
|------------------------------ |---------:|--------:|--------:|-------:|----------:|
| SamplerNotModifyingTraceState | 398.6 ns | 7.48 ns | 7.68 ns | 0.0782 |     328 B |
|    SamplerModifyingTraceState | 411.8 ns | 2.38 ns | 2.11 ns | 0.0782 |     328 B |
|    SamplerAppendingTraceState | 428.5 ns | 2.54 ns | 2.25 ns | 0.0916 |     384 B |

*/

namespace Benchmarks.Trace
{
    public class SamplerBenchmarks
    {
        private readonly ActivitySource sourceNotModifyTracestate = new("SamplerNotModifyingTraceState");
        private readonly ActivitySource sourceModifyTracestate = new("SamplerModifyingTraceState");
        private readonly ActivitySource sourceAppendTracestate = new("SamplerAppendingTraceState");
        private readonly ActivityContext parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "a=b", true);

        public SamplerBenchmarks()
        {
            var testSamplerNotModifyTracestate = new TestSampler
            {
                SamplingAction = (samplingParams) =>
                {
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            var testSamplerModifyTracestate = new TestSampler
            {
                SamplingAction = (samplingParams) =>
                {
                    return new SamplingResult(SamplingDecision.RecordAndSample, "a=b");
                },
            };

            var testSamplerAppendTracestate = new TestSampler
            {
                SamplingAction = (samplingParams) =>
                {
                    return new SamplingResult(SamplingDecision.RecordAndSample, samplingParams.ParentContext.TraceState + ",addedkey=bar");
                },
            };

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(testSamplerNotModifyTracestate)
                .AddSource(this.sourceNotModifyTracestate.Name)
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(testSamplerModifyTracestate)
                .AddSource(this.sourceModifyTracestate.Name)
                .Build();

            Sdk.CreateTracerProviderBuilder()
                .SetSampler(testSamplerAppendTracestate)
                .AddSource(this.sourceAppendTracestate.Name)
                .Build();
        }

        [Benchmark]
        public void SamplerNotModifyingTraceState()
        {
            using var activity = this.sourceNotModifyTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
        }

        [Benchmark]
        public void SamplerModifyingTraceState()
        {
            using var activity = this.sourceModifyTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
        }

        [Benchmark]
        public void SamplerAppendingTraceState()
        {
            using var activity = this.sourceAppendTracestate.StartActivity("Benchmark", ActivityKind.Server, this.parentContext);
        }

        internal class TestSampler : Sampler
        {
            public Func<SamplingParameters, SamplingResult> SamplingAction { get; set; }

            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                return this.SamplingAction?.Invoke(samplingParameters) ?? new SamplingResult(SamplingDecision.RecordAndSample);
            }
        }
    }
}
