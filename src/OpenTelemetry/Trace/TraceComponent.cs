// <copyright file="TraceComponent.cs" company="OpenTelemetry Authors">
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

    /// <summary>
    /// Trace component holds all the extensibility points required for distributed tracing.
    /// </summary>
    public sealed class TraceComponent : ITraceComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceComponent"/> class.
        /// </summary>
        public TraceComponent()
            : this(new SimpleEventQueue())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceComponent"/> class.
        /// </summary>
        /// <param name="eventQueue">Event queue to use before the exporter.</param>
        public TraceComponent(IEventQueue eventQueue)
        {
            this.TraceConfig = new Config.TraceConfig();

            // TODO(bdrutu): Add a config/argument for supportInProcessStores.
            if (eventQueue is SimpleEventQueue)
            {
                this.ExportComponent = Export.ExportComponent.CreateWithoutInProcessStores(eventQueue);
            }
            else
            {
                this.ExportComponent = Export.ExportComponent.CreateWithInProcessStores(eventQueue);
            }

            IStartEndHandler startEndHandler =
                new StartEndHandler(
                    this.ExportComponent.SpanExporter,
                    ((ExportComponent)this.ExportComponent).RunningSpanStore,
                    ((ExportComponent)this.ExportComponent).SampledSpanStore,
                    eventQueue);
            this.Tracer = new Tracer(startEndHandler, this.TraceConfig, null);
        }

        /// <inheritdoc/>
        public ITracer Tracer { get; }

        /// <inheritdoc/>
        public IExportComponent ExportComponent { get; }

        /// <inheritdoc/>
        public ITraceConfig TraceConfig { get; }
    }
}
