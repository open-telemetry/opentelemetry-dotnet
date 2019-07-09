// <copyright file="SpanBuilderBase.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using OpenTelemetry.Context;

    public abstract class SpanBuilderBase : ISpanBuilder
    {
        protected SpanBuilderBase(SpanKind kind)
        {
            this.Kind = kind;
        }

        private SpanBuilderBase()
        {
        }

        protected SpanKind Kind { get; private set; }

        public abstract ISpanBuilder SetSampler(ISampler sampler);

        public abstract ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks);

        public abstract ISpanBuilder SetRecordEvents(bool recordEvents);

        public abstract ISpan StartSpan();

        public IScope StartScopedSpan()
        {
            return CurrentSpanUtils.WithSpan(this.StartSpan(), true);
        }

        public IScope StartScopedSpan(out ISpan currentSpan)
        {
            currentSpan = this.StartSpan();
            return CurrentSpanUtils.WithSpan(currentSpan, true);
        }
    }
}
