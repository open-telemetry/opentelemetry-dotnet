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
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;
    using OpenTelemetry.Trace.Propagation;

    /// <summary>
    /// Helper class that provides easy to use static constructor of the default tracer component.
    /// </summary>
    public sealed class Tracing
    {
        private static Tracing tracing = new Tracing(true);

        private ITraceComponent traceComponent = null;

        internal Tracing(bool enabled)
        {
            if (enabled)
            {
                this.traceComponent = new TraceComponent(new RandomGenerator(), new SimpleEventQueue());
            }
            else
            {
                this.traceComponent = new NoopTraceComponent();
            }
        }

        /// <summary>
        /// Gets the tracer to record spans.
        /// </summary>
        public static ITracer Tracer => tracing.traceComponent.Tracer;

        /// <summary>
        /// Gets the propagation component that defines how to extract and inject span context from the wire protocols.
        /// </summary>
        public static IPropagationComponent PropagationComponent => tracing.traceComponent.PropagationComponent;

        /// <summary>
        /// Gets the export component to upload spans to.
        /// </summary>
        public static IExportComponent ExportComponent => tracing.traceComponent.ExportComponent;

        /// <summary>
        /// Gets the tracer configuration.
        /// </summary>
        public static ITraceConfig TraceConfig => tracing.traceComponent.TraceConfig;
    }
}
