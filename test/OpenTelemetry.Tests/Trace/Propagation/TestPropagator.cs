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
using OpenTelemetry.Context.Propagation;

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

        public ActivityContext Extract<T>(ActivityContext activityContext, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (this.defaultContext)
            {
                return activityContext;
            }

            IEnumerable<string> id = getter(carrier, this.idHeaderName);
            if (id.Count() <= 0)
            {
                return activityContext;
            }

            var traceparentParsed = TraceContextFormat.TryExtractTraceparent(id.First(), out var traceId, out var spanId, out var traceoptions);
            if (!traceparentParsed)
            {
                return activityContext;
            }

            string tracestate = string.Empty;
            IEnumerable<string> tracestateCollection = getter(carrier, this.stateHeaderName);
            if (tracestateCollection?.Any() ?? false)
            {
                TraceContextFormat.TryExtractTracestate(tracestateCollection.ToArray(), out tracestate);
            }

            return new ActivityContext(traceId, spanId, traceoptions, tracestate);
        }

        public void Inject<T>(ActivityContext activityContext, T carrier, Action<T, string, string> setter)
        {
            string headerNumber = this.stateHeaderName.Split('-').Last();

            var traceparent = string.Concat("00-", activityContext.TraceId.ToHexString(), "-", activityContext.SpanId.ToHexString());
            traceparent = string.Concat(traceparent, "-", headerNumber);

            setter(carrier, this.idHeaderName, traceparent);

            string tracestateStr = activityContext.TraceState;
            if (tracestateStr?.Length > 0)
            {
                setter(carrier, this.stateHeaderName, tracestateStr);
            }
        }

        public bool IsInjected<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            var traceparentCollection = getter(carrier, this.idHeaderName);

            // There must be a single traceparent
            return traceparentCollection != null && traceparentCollection.Count() == 1;
        }
    }
}
