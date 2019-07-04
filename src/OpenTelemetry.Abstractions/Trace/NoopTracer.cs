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
    using System;
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;

    /// <summary>
    /// No-op tracer.
    /// </summary>
    public sealed class NoopTracer : ITracer
    {
        /// <summary>
        /// Instance of the noop tracer.
        /// </summary>
        public static readonly NoopTracer Instance = new NoopTracer();

        internal NoopTracer()
        {
        }

        /// <inheritdoc/>
        public ISpan CurrentSpan => BlankSpan.Instance;

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat => new BinaryFormat();

        /// <inheritdoc/>
        public ITextFormat TextFormat => new TraceContextFormat();

        /// <inheritdoc/>
        public IScope WithSpan(ISpan span)
        {
            return NoopScope.Instance;
        }

        /// <inheritdoc/>
        public ISpanBuilder SpanBuilder(string spanName)
        {
            return new NoopSpanBuilder(spanName);
        }

        /// <inheritdoc/>
        public void RecordSpanData(SpanData span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }
        }
    }
}
