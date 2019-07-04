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
    using System;
    using System.Threading;
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;

    /// <inheritdoc/>
    public sealed class Tracer : ITracer
    {
        private readonly SpanBuilderOptions spanBuilderOptions;
        private readonly IExportComponent exportComponent;

        public Tracer(IRandomGenerator randomGenerator, IStartEndHandler startEndHandler, ITraceConfig traceConfig, IExportComponent exportComponent)
            : this(randomGenerator, startEndHandler, traceConfig, exportComponent, null, null)
        {
        }

        public Tracer(IRandomGenerator randomGenerator, IStartEndHandler startEndHandler, ITraceConfig traceConfig, IExportComponent exportComponent, IBinaryFormat binaryFormat, ITextFormat textFormat)
        {
            this.spanBuilderOptions = new SpanBuilderOptions(randomGenerator, startEndHandler, traceConfig);
            this.BinaryFormat = binaryFormat ?? new BinaryFormat();
            this.TextFormat = textFormat ?? new TraceContextFormat();
            this.exportComponent = exportComponent;
        }

        /// <inheritdoc/>
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan ?? BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat { get; }

        /// <inheritdoc/>
        public ITextFormat TextFormat { get; }

        /// <inheritdoc/>
        public IScope WithSpan(ISpan span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return CurrentSpanUtils.WithSpan(span, true);
        }

        /// <inheritdoc/>
        public ISpanBuilder SpanBuilder(string spanName)
        {
            return new SpanBuilder(spanName, this.spanBuilderOptions);
        }

        /// <inheritdoc/>
        public void RecordSpanData(SpanData span)
        {
            this.exportComponent.SpanExporter.ExportAsync(span, CancellationToken.None);
        }
    }
}
