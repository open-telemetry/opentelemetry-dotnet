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
    using OpenTelemetry.Trace.Propagation;

    /// <summary>
    /// Trace component holds all the extensibility points required for distributed tracing.
    /// </summary>
    public sealed class TraceComponent : ITraceComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceComponent"/> class.
        /// </summary>
        public TraceComponent()
            : this(new RandomGenerator(), new SimpleEventQueue())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceComponent"/> class.
        /// </summary>
        /// <param name="randomHandler">Random numbers generator.</param>
        /// <param name="eventQueue">Event queue to use before the exporter.</param>
        public TraceComponent(IRandomGenerator randomHandler, IEventQueue eventQueue)
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

            this.PropagationComponent = new DefaultPropagationComponent();
            IStartEndHandler startEndHandler =
                new StartEndHandler(
                    this.ExportComponent.SpanExporter,
                    this.ExportComponent.RunningSpanStore,
                    this.ExportComponent.SampledSpanStore,
                    eventQueue);
            this.Tracer = new Tracer(randomHandler, startEndHandler, this.TraceConfig);
        }

        /// <inheritdoc/>
        public ITracer Tracer { get; }

        /// <inheritdoc/>
        public IPropagationComponent PropagationComponent { get; }

        /// <inheritdoc/>
        public IExportComponent ExportComponent { get; }

        /// <inheritdoc/>
        public ITraceConfig TraceConfig { get; }
    }
}
