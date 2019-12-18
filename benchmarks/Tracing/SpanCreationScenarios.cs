// <copyright file="SpanCreationScenarios.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using OpenTelemetry.Trace;

namespace Benchmarks.Tracing
{
    internal class SpanCreationScenarios
    {
        public static ISpan CreateSpan(Tracer tracer)
        {
            var span = tracer.StartSpan("span");
            span.End();
            return span;
        }

        public static ISpan CreateSpan_Attributes(Tracer tracer)
        {
            var span = tracer.StartSpan("span");
            span.SetAttribute("attribute1", "1");
            span.SetAttribute("attribute2", 2);
            span.SetAttribute("attribute3", 3.0);
            span.SetAttribute("attribute4", false);
            span.End();
            return span;
        }

        public static ISpan CreateSpan_Propagate(Tracer tracer)
        {
            var span = tracer.StartSpan("span");
            using (tracer.WithSpan(span))
            {
            }

            span.End();
            return span;
        }

        public static ISpan CreateSpan_Active(Tracer tracer)
        {
            using (tracer.StartActiveSpan("span", out var span))
            {
                return span;
            }
        }

        public static ISpan CreateSpan_Active_GetCurrent(Tracer tracer)
        {
            ISpan span;
            
            using (tracer.StartActiveSpan("span", out _))
            {
                span = tracer.CurrentSpan;
            }

            return span;
        }
    }
}
