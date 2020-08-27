// <copyright file="TestPropagator.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class TestPropagator : ITextFormat
    {
        private readonly string idHeaderName;
        private readonly string stateHeaderName;
        private readonly bool defaultContext;

        public TestPropagator(string idHeaderName, string stateHeaderName, bool defaultContext = false)
        {
            this.idHeaderName = idHeaderName;
            this.stateHeaderName = stateHeaderName;
            this.defaultContext = defaultContext;
        }

        public ISet<string> Fields => new HashSet<string>() { this.idHeaderName, this.stateHeaderName };

        public PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (this.defaultContext)
            {
                return context;
            }

            IEnumerable<string> id = getter(carrier, this.idHeaderName);
            if (id.Count() <= 0)
            {
                return context;
            }

            var traceparentParsed = TextMapPropagator.TryExtractTraceparent(id.First(), out var traceId, out var spanId, out var traceoptions);
            if (!traceparentParsed)
            {
                return context;
            }

            string tracestate = string.Empty;
            IEnumerable<string> tracestateCollection = getter(carrier, this.stateHeaderName);
            if (tracestateCollection?.Any() ?? false)
            {
                TextMapPropagator.TryExtractTracestate(tracestateCollection.ToArray(), out tracestate);
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceoptions, tracestate),
                context.Baggage);
        }

        public void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            string headerNumber = this.stateHeaderName.Split('-').Last();

            var traceparent = string.Concat("00-", context.ActivityContext.TraceId.ToHexString(), "-", context.ActivityContext.SpanId.ToHexString());
            traceparent = string.Concat(traceparent, "-", headerNumber);

            setter(carrier, this.idHeaderName, traceparent);

            string tracestateStr = context.ActivityContext.TraceState;
            if (tracestateStr?.Length > 0)
            {
                setter(carrier, this.stateHeaderName, tracestateStr);
            }
        }
    }
}
