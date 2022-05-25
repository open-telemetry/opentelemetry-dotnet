// <copyright file="AspNetCoreExtractionBenchmark.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Benchmarks.Instrumentation
{
    public class AspNetCoreExtractionBenchmark
    {
        private const string Traceparent = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00";
        private static readonly Func<HttpRequest, string, IEnumerable<string>> CarrierGetter = (request, name) => request.Headers[name];
        private static readonly ActivityContext RandomActivityContext = new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        private static readonly ActivityContext MatchActivityContext = ActivityContext.Parse(Traceparent, traceState: null);
        private static readonly TextMapPropagator Propagator = new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
        });

        private readonly HttpRequest w3cExtractRequest;

        public AspNetCoreExtractionBenchmark()
        {
            HttpContext w3cContext = new DefaultHttpContext();
            this.w3cExtractRequest = w3cContext.Request;
            this.w3cExtractRequest.Headers.Add(TraceContextPropagator.TraceParent, Traceparent);
        }

        [Benchmark]
        [Arguments(true)]
        [Arguments(false)]
        public Activity AspNetCoreExtractBenchmark(bool match)
        {
            ActivityContext compareContext = match ? MatchActivityContext : RandomActivityContext;
            Activity createdActivity = null;

            var textMapPropagator = Propagator;
            if (textMapPropagator is not TraceContextPropagator)
            {
                PropagationContext ctx = default;
                textMapPropagator.Extract(ref ctx, this.w3cExtractRequest, CarrierGetter);

                ref readonly ActivityContext activityContext = ref PropagationContext.GetActivityContextRef(in ctx);

                if (ActivityContextExtensions.IsValid(in activityContext)
                    && (!activityContext.TraceId.Equals(compareContext.TraceId)
                    || !activityContext.SpanId.Equals(compareContext.SpanId)
                    || activityContext.TraceFlags != compareContext.TraceFlags
                    || activityContext.TraceState != compareContext.TraceState))
                {
                    createdActivity = new Activity("InnerActivity");
                    createdActivity.SetParentId(activityContext.TraceId, activityContext.SpanId, activityContext.TraceFlags);
                    createdActivity.TraceStateString = activityContext.TraceState;
                }

                Baggage.Current = ctx.Baggage;
            }

            return createdActivity;
        }
    }
}
#endif
