// <copyright file="NoopSpanBuilder.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Trace.Internal;

    public class NoopSpanBuilder : SpanBuilderBase
    {
        private NoopSpanBuilder(string name, SpanKind kind) : base(kind)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
        }

        public override ISpan StartSpan()
        {
            return BlankSpan.Instance;
        }

        public override ISpanBuilder SetSampler(ISampler sampler)
        {
            return this;
        }

        public override ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks)
        {
            return this;
        }

        public override ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            return this;
        }

        internal static ISpanBuilder CreateWithParent(string spanName, SpanKind kind, ISpan parent = null)
        {
            return new NoopSpanBuilder(spanName, kind);
        }

        internal static ISpanBuilder CreateWithRemoteParent(string spanName, SpanKind kind, ISpanContext remoteParentSpanContext = null)
        {
            return new NoopSpanBuilder(spanName, kind);
        }
    }
}
