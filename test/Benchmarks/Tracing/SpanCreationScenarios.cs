// <copyright file="SpanCreationScenarios.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Benchmarks
{
    internal class SpanCreationScenarios
    {
        public static void CreateSpan(Tracer tracer)
        {
            using var span = tracer.StartSpan("span");
            span.End();
        }

        public static void CreateSpan_ParentContext(Tracer tracer)
        {
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, true);
            using var span = tracer.StartSpan("span", SpanKind.Client, parentContext);
            span.End();
        }

        public static void CreateSpan_Attributes(Tracer tracer)
        {
            using var span = tracer.StartSpan("span");
            span.SetAttribute("string", "string");
            span.SetAttribute("int", 1);
            span.SetAttribute("long", 1L);
            span.SetAttribute("bool", false);
            span.End();
        }

        public static void CreateSpan_Propagate(Tracer tracer)
        {
            using var span = tracer.StartSpan("span");
            using (Tracer.WithSpan(span))
            {
            }

            span.End();
        }

        public static void CreateSpan_Active(Tracer tracer)
        {
            using var span = tracer.StartSpan("span");
        }

        public static void CreateSpan_Active_GetCurrent(Tracer tracer)
        {
            TelemetrySpan span;

            using (tracer.StartSpan("span"))
            {
                span = Tracer.CurrentSpan;
            }
        }
    }
}
