namespace LoggingTracer
{
    using System.Collections.Generic;
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

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, ISpan parent) : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName}, {spanKind}, {parent})");
            this.parent = parent;
        }

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, SpanContext remoteParentSpanContext) : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName}, {spanKind}, {remoteParentSpanContext})");
            this.remoteParentSpanContext = remoteParentSpanContext;
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

        public ISpanBuilder SetSampler(ISampler sampler)
        {
            Logger.Log($"SpanBuilder.SetSampler({sampler})");
            return this;
        }

        public IScope StartScopedSpan()
        {
            Logger.Log($"SpanBuilder.StartScopedSpan()");
            return new CurrentSpanUtils.LoggingScope(this.span);
        }

        public IScope StartScopedSpan(out ISpan currentSpan)
        {
            Logger.Log($"SpanBuilder.StartScopedSpan()");
            return new CurrentSpanUtils.LoggingScope(currentSpan = this.span);
        }

        public ISpan StartSpan()
        {
            Logger.Log($"SpanBuilder.StartSpan()");
            return this.span;
        }
    }
}
