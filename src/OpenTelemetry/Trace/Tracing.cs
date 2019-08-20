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
    using System.Runtime.CompilerServices;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;

    /// <summary>
    /// Class that manages a global instance of the <see cref="Tracer"/>.
    /// </summary>
    public sealed class Tracing
    {
        private static TracerFactory tracerFactory;
        private static Tracer tracer;

        internal Tracing()
        {
            IEventQueue eventQueue = new SimpleEventQueue();
            TraceConfig = new Config.TraceConfig();

            SpanExporter = Export.SpanExporter.Create();

            IStartEndHandler startEndHandler =
                new StartEndHandler(
                    SpanExporter,
                    eventQueue);

            tracer = new Tracer(startEndHandler, TraceConfig);
            tracerFactory = new TracerFactory(tracer);
        }

        /// <summary>   
        /// Gets the factory for creating named Tracers.
        /// </summary>
        public static ITracerFactory TracerFactory => tracerFactory;
        
        /// <summary>   
        /// Gets the tracer to record spans.
        /// </summary>
        public static ITracer Tracer => tracer; // TODO: We could probably get rid of this property in favor of TracerFactory.

        /// <summary>
        /// Gets the exporter to use to upload spans.
        /// </summary>
        public static ISpanExporter SpanExporter { get; private set; }

        /// <summary>
        /// Gets the trace config.
        /// </summary>
        public static ITraceConfig TraceConfig { get; private set; }
    }
}
