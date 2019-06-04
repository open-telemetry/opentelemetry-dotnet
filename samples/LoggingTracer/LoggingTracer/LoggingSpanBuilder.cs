using System.Collections.Generic;
using OpenTelemetry.Common;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingSpanBuilder : ISpanBuilder
    {
        private string spanName;
        private SpanKind spanKind;
        private ISpan parent;
        private SpanContext remoteParentSpanContext;

        public LoggingSpanBuilder(string spanName, SpanKind spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName})");

            this.spanName = spanName;
            this.spanKind = spanKind;
        }

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, ISpan parent) : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName})");
            this.parent = parent;
        }

        public LoggingSpanBuilder(string spanName, SpanKind spanKind, SpanContext remoteParentSpanContext) : this(spanName, spanKind)
        {
            Logger.Log($"SpanBuilder.ctor({spanName})");
            this.remoteParentSpanContext = remoteParentSpanContext;
        }

        public ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks)
        {
            Logger.Log($"SpanBuilder.SetParentLinks");
            return this;
        }

        public ISpanBuilder SetRecordEvents(bool recordEvents)
        {
            Logger.Log($"SpanBuilder.SetRecordEvents");
            return this;
        }

        public ISpanBuilder SetSampler(ISampler sampler)
        {
            Logger.Log($"SpanBuilder.SetSampler");
            return this;
        }

        public IScope StartScopedSpan()
        {
            Logger.Log($"SpanBuilder.StartScopedSpan");
            return new CurrentSpanUtils.LoggingScope(new LoggingSpan(spanName, spanKind));
        }

        public IScope StartScopedSpan(out ISpan currentSpan)
        {
            Logger.Log($"SpanBuilder.StartScopedSpan");
            return new CurrentSpanUtils.LoggingScope(currentSpan = new LoggingSpan(spanName, spanKind));
        }

        public ISpan StartSpan()
        {
            Logger.Log($"SpanBuilder.StartSpan");
            return new LoggingSpan(spanName, spanKind);
        }
    }
}


