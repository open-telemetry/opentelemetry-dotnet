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
    public sealed class NoopTracer : TracerBase, ITracer
    {
        internal NoopTracer()
        {
        }

        public override ISpanBuilder SpanBuilderWithExplicitParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpan parent = null)
        {
            return NoopSpanBuilder.CreateWithParent(spanName, spanKind, parent);
        }

        public override ISpanBuilder SpanBuilderWithRemoteParent(string spanName, SpanKind spanKind = SpanKind.Unspecified, ISpanContext remoteParentSpanContext = null)
        {
            return NoopSpanBuilder.CreateWithRemoteParent(spanName, spanKind, remoteParentSpanContext);
        }
    }
}
