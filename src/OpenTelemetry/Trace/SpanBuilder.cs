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
    using System.Diagnostics;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;

    /// <inheritdoc/>
    public class SpanBuilder : ISpanBuilder
    {
        private readonly SpanBuilderOptions options;
        private readonly string name;

        private SpanKind kind;
        private ISpan parent;
        private SpanContext parentSpanContext;
        private ParentType parentType = ParentType.CurrentSpan;
        private ISampler sampler;
        private List<ILink> links;
        private bool recordEvents;
        private Timer timestampConverter;

        internal SpanBuilder(string name, SpanBuilderOptions options)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            this.options = options;
        }

        private enum ParentType
        {
            CurrentSpan,
            ExplicitParent,
            ExplicitRemoteParent,
            NoParent,
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSampler(ISampler sampler)
        {
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(ISpan parent)
        {
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.parentType = ParentType.ExplicitParent;
            this.timestampConverter = ((Span)parent)?.TimestampConverter;
            this.parentSpanContext = null;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetParent(SpanContext remoteParent)
        {
            this.parentSpanContext = remoteParent ?? throw new ArgumentNullException(nameof(remoteParent));
            this.parent = null;
            this.parentType = ParentType.ExplicitRemoteParent;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetNoParent()
        {
            this.parentType = ParentType.NoParent;
            this.parentSpanContext = null;
            this.parentSpanContext = null;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetSpanKind(SpanKind spanKind)
        {
            this.kind = spanKind;
            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext spanContext)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            return this.AddLink(Link.FromSpanContext(spanContext));
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(SpanContext spanContext, IDictionary<string, IAttributeValue> attributes)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            return this.AddLink(Link.FromSpanContext(spanContext, attributes));
        }

        /// <inheritdoc/>
        public ISpanBuilder AddLink(ILink link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            if (this.links == null)
            {
                this.links = new List<ILink>();
            }

            this.links.Add(link);

            return this;
        }

        /// <inheritdoc/>
        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            this.recordEvents = recordEvents;
            return this;
        }

        /// <inheritdoc/>
        public ISpan StartSpan()
        {
            SpanContext parentContext = FindParent(this.parentType, this.parent, this.parentSpanContext);
            var activeTraceParams = this.options.TraceConfig.ActiveTraceParams;
            ActivityTraceId traceId;
            var spanId = ActivitySpanId.CreateRandom();

            ActivitySpanId parentSpanId = default;
            ActivityTraceFlags traceOptions = ActivityTraceFlags.None;

            if (parentContext == null || !parentContext.IsValid)
            {
                // New root span.
                traceId = ActivityTraceId.CreateRandom();
            }
            else
            {
                // New child span.
                traceId = parentContext.TraceId;
                parentSpanId = parentContext.SpanId;
                traceOptions = parentContext.TraceOptions;
            }

            bool sampledIn = MakeSamplingDecision(
                    parentContext,
                    this.name,
                    this.sampler,
                    this.links,
                    traceId,
                    spanId,
                    activeTraceParams);

            var spanOptions = SpanOptions.None;
            if (sampledIn || this.recordEvents)
            {
                traceOptions |= ActivityTraceFlags.Recorded;
                spanOptions = SpanOptions.RecordEvents;
            }
            else
            {
                traceOptions &= ~ActivityTraceFlags.Recorded;
            }

            var span = Span.StartSpan(
                        SpanContext.Create(traceId, spanId, traceOptions, parentContext?.Tracestate ?? Tracestate.Empty),
                        spanOptions,
                        this.name,
                        this.kind,
                        parentSpanId,
                        activeTraceParams,
                        this.options.StartEndHandler,
                        this.timestampConverter);
            LinkSpans(span, this.links);
            return span;
        }

        private static bool IsAnyParentLinkSampled(List<ILink> parentLinks)
        {
            if (parentLinks != null)
            {
                foreach (var parentLink in parentLinks)
                {
                    if ((parentLink.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void LinkSpans(ISpan span, List<ILink> parentLinks)
        {
            if (parentLinks != null)
            {
                foreach (var link in parentLinks)
                {
                    span.AddLink(link);
                }
            }
        }

        private static bool MakeSamplingDecision(
            SpanContext parent,
            string name,
            ISampler sampler,
            List<ILink> parentLinks,
            ActivityTraceId traceId,
            ActivitySpanId spanId,
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
            return (parent.TraceOptions & ActivityTraceFlags.Recorded) != 0 || IsAnyParentLinkSampled(parentLinks);
        }

        private static SpanContext FindParent(ParentType parentType, ISpan explicitParent, SpanContext remoteParent)
        {
            switch (parentType)
            {
                case ParentType.NoParent:
                    return null;
                case ParentType.CurrentSpan:
                    ISpan currentSpan = CurrentSpanUtils.CurrentSpan;
                    return currentSpan?.Context;
                case ParentType.ExplicitParent:
                    return explicitParent?.Context;
                case ParentType.ExplicitRemoteParent:
                    return remoteParent;
                default:
                    throw new ArgumentException($"Unknown parentType {parentType}");
            }
        }
    }
}
