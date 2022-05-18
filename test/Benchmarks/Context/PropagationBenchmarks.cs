// <copyright file="PropagationBenchmarks.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Context
{
    public class PropagationBenchmarks
    {
        private static readonly Func<HttpRequest, string, IEnumerable<string>> CarrierGetter = (request, name) => request.Headers[name];
        private static readonly Action<object, string, string> CarrierSetter = (request, name, value) => { };
        private readonly TraceContextPropagator traceContextPropagator = new();
        private readonly TextMapPropagator compositePropagator = new CompositeTextMapPropagator(new TextMapPropagator[] { new TraceContextPropagator(), new BaggagePropagator(), new B3Propagator() });
        private readonly object injectRequest = new();
        private readonly PropagationContext injectContext = new(
            new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
            default);

        private HttpRequest extractRequest;

        [Params("Empty", "W3C", "B3")]
        public string Mode { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            HttpContext context = new DefaultHttpContext();

            this.extractRequest = context.Request;

            switch (this.Mode)
            {
                case "W3C":
                    this.extractRequest.Headers.Add(TraceContextPropagator.TraceParent, "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00");
                    break;
                case "B3":
                    this.extractRequest.Headers.Add(B3Propagator.XB3TraceId, "ff000000000000000000000000000041");
                    this.extractRequest.Headers.Add(B3Propagator.XB3SpanId, "ff00000000000041");
                    this.extractRequest.Headers.Add(B3Propagator.XB3Sampled, "1");
                    break;
            }
        }

        [Benchmark]
        public void TraceContextPropagator_ExtractBenchmark()
        {
            PropagationContext context = default;
            this.traceContextPropagator.Extract(ref context, this.extractRequest, CarrierGetter);
        }

        [Benchmark]
        public void TraceContextPropagator_InjectBenchmark()
        {
            this.traceContextPropagator.Inject(in this.injectContext, this.injectRequest, CarrierSetter);
        }

        [Benchmark]
        public void CompositeTextMapPropagator_ExtractBenchmark()
        {
            PropagationContext context = default;
            this.compositePropagator.Extract(ref context, this.extractRequest, CarrierGetter);
        }

        [Benchmark]
        public void CompositeTextMapPropagator_InjectBenchmark()
        {
            this.traceContextPropagator.Inject(in this.injectContext, this.injectRequest, CarrierSetter);
        }
    }
}
#endif
