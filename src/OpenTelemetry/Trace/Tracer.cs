// <copyright file="Tracer.cs" company="OpenTelemetry Authors">
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
    using System.Threading;
    using OpenTelemetry.Common;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;

    /// <inheritdoc/>
    public sealed class Tracer : TracerBase
    {
        private const int ExporterBufferSize = 32;

        // Enforces that trace export exports data at least once every 5 seconds.
        private static readonly Duration ExporterScheduleDelay = Duration.Create(5, 0);

        private readonly SpanBuilderOptions spanBuilderOptions;
        private readonly SpanExporter spanExporter;
        private readonly IBinaryFormat binaryFormat;
        private readonly ITextFormat textFormat;

        /// <summary>
        /// Creates an instance of <see cref="ITracer"/>.
        /// </summary>
        /// <param name="randomGenerator">Span id generator.</param>
        /// <param name="startEndHandler">Start/end event handler.</param>
        /// <param name="traceConfig">Trace configuration.</param>
        public Tracer(IRandomGenerator randomGenerator, IStartEndHandler startEndHandler, ITraceConfig traceConfig)
            : this(randomGenerator, startEndHandler, traceConfig, null, null, null)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="ITracer"/>.
        /// </summary>
        /// <param name="randomGenerator">Span id generator.</param>
        /// <param name="startEndHandler">Start/end event handler.</param>
        /// <param name="traceConfig">Trace configuration.</param>
        /// <param name="spanExporter">Exporter for span.</param>
        /// <param name="binaryFormat">Binary format context propagator.</param>
        /// <param name="textFormat">Text format context propagator.</param>
        public Tracer(IRandomGenerator randomGenerator, IStartEndHandler startEndHandler, ITraceConfig traceConfig, SpanExporter spanExporter, IBinaryFormat binaryFormat, ITextFormat textFormat)
        {
            this.spanBuilderOptions = new SpanBuilderOptions(randomGenerator, startEndHandler, traceConfig);
            this.spanExporter = spanExporter ?? (SpanExporter)SpanExporter.Create(ExporterBufferSize, ExporterScheduleDelay);
            this.binaryFormat = binaryFormat ?? new BinaryFormat();
            this.textFormat = textFormat ?? new TraceContextFormat();
        }

        /// <inheritdoc/>
        public override IBinaryFormat BinaryFormat => this.binaryFormat;

        /// <inheritdoc/>
        public override ITextFormat TextFormat => this.textFormat;

        /// <inheritdoc/>
        public override void RecordSpanData(SpanData span)
        {
            this.spanExporter.ExportAsync(span, CancellationToken.None);
        }

        /// <inheritdoc/>
        public override ISpanBuilder SpanBuilderWithParent(string name, SpanKind kind = SpanKind.Internal, ISpan parent = null)
        {
            return Trace.SpanBuilder.Create(name, kind, parent, this.spanBuilderOptions);
        }

        /// <inheritdoc/>
        public override ISpanBuilder SpanBuilderWithParentContext(string name, SpanKind kind = SpanKind.Internal, SpanContext parentContext = null)
        {
            return Trace.SpanBuilder.Create(name, kind, parentContext, this.spanBuilderOptions);
        }
    }
}
