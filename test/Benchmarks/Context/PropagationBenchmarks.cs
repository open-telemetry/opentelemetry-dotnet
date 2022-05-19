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
        private readonly B3Propagator b3Propagator = new();
        private readonly object injectRequest = new();
        private readonly HttpRequest emptyExtractRequest;
        private readonly HttpRequest w3cExtractRequest;
        private readonly HttpRequest b3ExtractRequest;

        private readonly TextMapPropagator compositePropagator = new CompositeTextMapPropagator(
            new TextMapPropagator[] { new TraceContextPropagator(), new BaggagePropagator(), new B3Propagator() });

        private readonly PropagationContext injectContext = new(
            new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
            default);

        public PropagationBenchmarks()
        {
            HttpContext emptyContext = new DefaultHttpContext();
            this.emptyExtractRequest = emptyContext.Request;

            HttpContext w3cContext = new DefaultHttpContext();
            this.w3cExtractRequest = w3cContext.Request;
            this.w3cExtractRequest.Headers.Add(TraceContextPropagator.TraceParent, "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00");

            HttpContext b3Context = new DefaultHttpContext();
            this.b3ExtractRequest = b3Context.Request;
            this.b3ExtractRequest.Headers.Add(B3Propagator.XB3TraceId, "ff000000000000000000000000000041");
            this.b3ExtractRequest.Headers.Add(B3Propagator.XB3SpanId, "ff00000000000041");
            this.b3ExtractRequest.Headers.Add(B3Propagator.XB3Sampled, "1");
        }

        [Benchmark]
        [Arguments("Empty")]
        [Arguments("W3C")]
        public void TraceContextPropagator_ExtractBenchmark(string mode)
        {
            PropagationContext context = default;
            this.traceContextPropagator.Extract(ref context, mode == "Empty" ? this.emptyExtractRequest : this.w3cExtractRequest, CarrierGetter);
        }

        [Benchmark]
        public void TraceContextPropagator_InjectBenchmark()
        {
            this.traceContextPropagator.Inject(in this.injectContext, this.injectRequest, CarrierSetter);
        }

        [Benchmark]
        [Arguments("Empty")]
        [Arguments("B3")]
        public void B3Propagator_ExtractBenchmark(string mode)
        {
            PropagationContext context = default;
            this.b3Propagator.Extract(ref context, mode == "Empty" ? this.emptyExtractRequest : this.b3ExtractRequest, CarrierGetter);
        }

        [Benchmark]
        public void B3Propagator_InjectBenchmark()
        {
            this.b3Propagator.Inject(in this.injectContext, this.injectRequest, CarrierSetter);
        }

        [Benchmark]
        [Arguments("Empty")]
        [Arguments("W3C")]
        [Arguments("B3")]
        public void CompositeTextMapPropagator_ExtractBenchmark(string mode)
        {
            var request = mode switch
            {
                "W3C" => this.w3cExtractRequest,
                "B3" => this.b3ExtractRequest,
                _ => this.emptyExtractRequest,
            };

            PropagationContext context = default;
            this.compositePropagator.Extract(ref context, request, CarrierGetter);
        }

        [Benchmark]
        public void CompositeTextMapPropagator_InjectBenchmark()
        {
            this.compositePropagator.Inject(in this.injectContext, this.injectRequest, CarrierSetter);
        }
    }
}
#endif
