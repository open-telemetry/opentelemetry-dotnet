// <copyright file="NoopTracer.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// No-op tracer.
    /// </summary>
    public sealed class NoopTracer : TracerBase, ITracer
    {
        private static readonly IBinaryFormat BinaryFormatValue = new BinaryFormat();
        private static readonly ITextFormat TextFormatValue = new TraceContextFormat();

        internal NoopTracer()
        {
        }

        /// <inheritdoc/>
        public override IBinaryFormat BinaryFormat
        {
            get
            {
                return BinaryFormatValue;
            }
        }

        /// <inheritdoc/>
        public override ITextFormat TextFormat
        {
            get
            {
                return TextFormatValue;
            }
        }

        /// <inheritdoc/>
        public override ISpanBuilder SpanBuilderWithParent(string name, SpanKind kind = SpanKind.Internal, ISpan parent = null)
        {
            return NoopSpanBuilder.SetParent(name, kind, parent);
        }

        /// <inheritdoc/>
        public override ISpanBuilder SpanBuilderWithParentContext(string name, SpanKind kind = SpanKind.Internal, SpanContext parentContext = null)
        {
            return NoopSpanBuilder.SetParent(name, kind, parentContext);
        }

        /// <inheritdoc/>
        public override void RecordSpanData(SpanData span)
        {
        }
    }
}
