// <copyright file="SpanBuilder.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;

    /// <inheritdoc/>
    public class SpanBuilder : SpanBuilderBase
    {
        private SpanBuilder(string name, SpanKind kind, SpanBuilderOptions options, ISpanContext parentContext = null, ISpan parent = null) : base(kind)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Parent = parent;
            this.ParentSpanContext = parentContext;
            this.Options = options;
        }

        private SpanBuilderOptions Options { get; set; }

        private string Name { get; set; }

        private ISpan Parent { get; set; }

        private ISpanContext ParentSpanContext { get; set; }

        private ISampler Sampler { get; set; }

        private IEnumerable<ISpan> ParentLinks { get; set; } = Enumerable.Empty<ISpan>();

        private bool RecordEvents { get; set; }

        /// <inheritdoc/>
        public override ISpan StartSpan()
        {
            ISpanContext parentContext = this.ParentSpanContext;
            Timer timestampConverter = null;
            if (this.ParentSpanContext == null)
            {
                // This is not a child of a remote Span. Get the parent SpanContext from the parent Span if
                // any.
                ISpan parent = this.Parent;
                if (parent != null)
                {
                    parentContext = parent.Context;

                    // Pass the timestamp converter from the parent to ensure that the recorded events are in
                    // the right order. Implementation uses System.nanoTime() which is monotonically increasing.
                    if (parent is Span)
                    {
                        timestampConverter = ((Span)parent).TimestampConverter;
                    }
                }
            }

            return this.StartSpanInternal(
                parentContext,
                this.Name,
                this.Sampler,
                this.ParentLinks,
                this.RecordEvents,
                timestampConverter);
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetSampler(ISampler sampler)
        {
            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks)
        {
            this.ParentLinks = parentLinks ?? throw new ArgumentNullException(nameof(parentLinks));
            return this;
        }

        /// <inheritdoc/>
        public override ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            this.RecordEvents = recordEvents;
            return this;
        }

        internal static ISpanBuilder Create(string name, SpanKind kind, ISpan parent, SpanBuilderOptions options)
        {
            return new SpanBuilder(name, kind, options, null, parent);
        }

        internal static ISpanBuilder Create(string name, SpanKind kind, ISpanContext parentContext, SpanBuilderOptions options)
        {
            return new SpanBuilder(name, kind, options, parentContext, null);
        }

        private static bool IsAnyParentLinkSampled(IEnumerable<ISpan> parentLinks)
        {
            foreach (ISpan parentLink in parentLinks)
            {
                if (parentLink.Context.TraceOptions.IsSampled)
                {
                    return true;
                }
            }

            return false;
        }

        private static void LinkSpans(ISpan span, IEnumerable<ISpan> parentLinks)
        {
            if (parentLinks.Any())
            {
                ILink childLink = Link.FromSpanContext(span.Context, LinkType.ChildLinkedSpan);
                foreach (ISpan linkedSpan in parentLinks)
                {
                    linkedSpan.AddLink(childLink);
                    span.AddLink(Link.FromSpanContext(linkedSpan.Context, LinkType.ParentLinkedSpan));
                }
            }
        }

        private static bool MakeSamplingDecision(
            ISpanContext parent,
            string name,
            ISampler sampler,
            IEnumerable<ISpan> parentLinks,
            ITraceId traceId,
            ISpanId spanId,
            ITraceParams activeTraceParams)
        {
            // If users set a specific sampler in the SpanBuilder, use it.
            if (sampler != null)
            {
                return sampler.ShouldSample(parent, traceId, spanId, name, parentLinks);
            }

            // Use the default sampler if this is a root Span or this is an entry point Span (has remote
            // parent).
            if (parent == null || !parent.IsValid)
            {
                return activeTraceParams
                    .Sampler
                    .ShouldSample(parent, traceId, spanId, name, parentLinks);
            }

            // Parent is always different than null because otherwise we use the default sampler.
            return parent.TraceOptions.IsSampled || IsAnyParentLinkSampled(parentLinks);
        }

        private ISpan StartSpanInternal(
                     ISpanContext parent,
                     string name,
                     ISampler sampler,
                     IEnumerable<ISpan> parentLinks,
                     bool recordEvents,
                     Timer timestampConverter)
        {
            ITraceParams activeTraceParams = this.Options.TraceConfig.ActiveTraceParams;
            IRandomGenerator random = this.Options.RandomHandler;
            ITraceId traceId;
            ISpanId spanId = SpanId.GenerateRandomId(random);
            ISpanId parentSpanId = null;
            TraceOptionsBuilder traceOptionsBuilder;
            if (parent == null || !parent.IsValid)
            {
                // New root span.
                traceId = TraceId.GenerateRandomId(random);
                traceOptionsBuilder = TraceOptions.Builder();
            }
            else
            {
                // New child span.
                traceId = parent.TraceId;
                parentSpanId = parent.SpanId;
                traceOptionsBuilder = TraceOptions.Builder(parent.TraceOptions);
            }

            traceOptionsBuilder.SetIsSampled(
                 MakeSamplingDecision(
                    parent,
                    name,
                    sampler,
                    parentLinks,
                    traceId,
                    spanId,
                    activeTraceParams));
            TraceOptions traceOptions = traceOptionsBuilder.Build();
            SpanOptions spanOptions = SpanOptions.None;

            if (traceOptions.IsSampled || recordEvents)
            {
                spanOptions = SpanOptions.RecordEvents;
            }

            ISpan span = Span.StartSpan(
                        SpanContext.Create(traceId, spanId, traceOptions, parent?.Tracestate ?? Tracestate.Empty),
                        spanOptions,
                        name,
                        this.Kind,
                        parentSpanId,
                        activeTraceParams,
                        this.Options.StartEndHandler,
                        timestampConverter);
            LinkSpans(span, parentLinks);
            return span;
        }
    }
}
