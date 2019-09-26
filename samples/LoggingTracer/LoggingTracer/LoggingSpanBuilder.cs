// <copyright file="LoggingSpanBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Context;
    using OpenTelemetry.Trace;

    public class LoggingSpanBuilder : ISpanBuilder
    {
        private string spanName;
        private SpanKind spanKind;
        private ISpan parent;
        private SpanContext remoteParentSpanContext;
        private ISpan span;

        public LoggingSpanBuilder(string spanName, SpanKind spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName})");
            this.spanName = spanName;
            this.spanKind = spanKind;
            this.span = new LoggingSpan(spanName, spanKind);
        }

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, ISpan parent)
            : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName}, {spanKind}, {parent})");
            this.parent = parent;
        }

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, SpanContext remoteParentSpanContext)
            : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName}, {spanKind}, {remoteParentSpanContext})");
            this.remoteParentSpanContext = remoteParentSpanContext;
        }

        public ISpanBuilder AddLink(SpanContext spanContext)
        {
            Logger.Log($"SpanBuilder.AddLink({spanContext})");
            return this;
        }

        public ISpanBuilder AddLink(Activity activity)
        {
            Logger.Log($"SpanBuilder.AddLink({activity})");
            return this;
        }

        public ISpanBuilder AddLink(ILink link)
        {
            Logger.Log($"SpanBuilder.AddLink({link})");
            return this;
        }

        public ISpanBuilder AddLink(SpanContext context, IDictionary<string, object> attributes)
        {
            Logger.Log($"SpanBuilder.AddLink({context}, {attributes.Count})");
            return this;
        }

        public ISpanBuilder SetCreateChild(bool createChild)
        {
            Logger.Log($"SpanBuilder.SetCreateChild({createChild})");
            return this;
        }

        public ISpanBuilder SetNoParent()
        {
            Logger.Log("SpanBuilder.SetNoParent()");
            return this;
        }

        public ISpanBuilder SetParent(ISpan parent)
        {
            Logger.Log($"SpanBuilder.SetParent({parent})");
            return this;
        }

        public ISpanBuilder SetParent(Activity parent)
        {
            Logger.Log($"SpanBuilder.SetParent({parent})");
            return this;
        }

        public ISpanBuilder SetParent(SpanContext remoteParent)
        {
            Logger.Log($"SpanBuilder.SetParent({remoteParent})");
            return this;
        }

        public ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks)
        {
            Logger.Log($"SpanBuilder.SetParentLinks(parentLinks: {parentLinks.Count()})");
            return this;
        }

        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            Logger.Log($"SpanBuilder.SetRecordEvents({recordEvents})");
            return this;
        }

        public ISpanBuilder SetStartTimestamp(DateTimeOffset startTimestamp)
        {
            Logger.Log($"SpanBuilder.SetStartTimestamp({startTimestamp})");
            return this;
        }

        public ISpanBuilder SetSampler(ISampler sampler)
        {
            Logger.Log($"SpanBuilder.SetSampler({sampler})");
            return this;
        }

        public ISpanBuilder SetSpanKind(SpanKind spanKind)
        {
            Logger.Log($"SpanBuilder.SetSpanKind({spanKind})");
            return this;
        }

        public IScope StartScopedSpan()
        {
            Logger.Log("SpanBuilder.StartScopedSpan()");
            return new CurrentSpanUtils.LoggingScope(this.span);
        }

        public IScope StartScopedSpan(out ISpan currentSpan)
        {
            Logger.Log("SpanBuilder.StartScopedSpan()");
            return new CurrentSpanUtils.LoggingScope(currentSpan = this.span);
        }

        public ISpan StartSpan()
        {
            Logger.Log("SpanBuilder.StartSpan()");
            return this.span;
        }
    }
}
