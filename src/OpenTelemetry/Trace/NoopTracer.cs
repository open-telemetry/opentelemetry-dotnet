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

using System;
using System.Diagnostics;
using OpenTelemetry.Context;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace.Internal;

namespace OpenTelemetry.Trace
{
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// No-op tracer.
    /// </summary>
    public sealed class NoopTracer : TracerBase
    {
        private static IBinaryFormat binaryFormat = new BinaryFormat();
        private static ITextFormat textFormat = new TraceContextFormat();

        internal NoopTracer()
        {
        }

        public override ISpan CurrentSpan => BlankSpan.Instance;

        /// <inheritdoc/>
        public override IBinaryFormat BinaryFormat => binaryFormat;

        /// <inheritdoc/>
        public override ITextFormat TextFormat => textFormat;

        public override IScope WithSpan(ISpan span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return NoopScope.Instance;
        }

        public override ISpanBuilder SpanBuilder(string name, SpanKind kind = SpanKind.Internal)
        {
            return NoopSpanBuilder.SetParent(name, kind, parent:null);
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

        public override ISpanBuilder SpanBuilderFromActivity(string name, SpanKind kind = SpanKind.Internal, Activity activity = null)
        {
            return NoopSpanBuilder.SetParent(name, kind, activity);
        }

        /// <inheritdoc/>
        public override void RecordSpanData(SpanData span)
        {
        }
    }
}
