// <copyright file="Tracing.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;

    /// <summary>
    /// Class that manages a global instance of the <see cref="Tracer"/>.
    /// </summary>
    public sealed class Tracing
    {
        private static Tracing tracingValue = new Tracing();
        private static Tracer tracer;

        internal Tracing()
        {
            TraceConfig = TraceConfig.Default;

            SpanProcessor = new BatchingSpanProcessor(new NoopSpanExporter());

            tracer = new Tracer(SpanProcessor, TraceConfig);
        }

        /// <summary>   
        /// Gets the tracer to record spans.
        /// </summary>
        public static ITracer Tracer => (ITracer)tracer;

        /// <summary>
        /// Gets the exporter to use to upload spans.
        /// </summary>
        public static SpanProcessor SpanProcessor { get; private set; }

        /// <summary>
        /// Gets the trace config.
        /// </summary>
        public static TraceConfig TraceConfig { get; private set; }
    }
}
