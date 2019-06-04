using OpenTelemetry.Common;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingTracer : ITracer
    {
        public ISpan CurrentSpan
        {
            get
            {
                return CurrentSpanUtils.CurrentSpan;
            }
        }

        public void RecordSpanData(ISpanData span)
        {
            Logger.Log($"Tracer.RecordSpanData");
        }

        public ISpanBuilder SpanBuilder(string spanName, SpanKind spanKind = SpanKind.Internal)
        {
            Logger.Log($"Tracer.SpanBuilder");
            return new LoggingSpanBuilder(spanName, spanKind);
        }

        public ISpanBuilder SpanBuilderWithParent(string spanName, SpanKind spanKind = SpanKind.Internal, ISpan parent = null)
        {
            Logger.Log($"Tracer.SpanBuilderWithExplicitParent");
            return new LoggingSpanBuilder(spanName, spanKind, parent);
        }

        public ISpanBuilder SpanBuilderWithParentContext(string spanName, SpanKind spanKind = SpanKind.Internal, SpanContext remoteParentSpanContext = null)
        {
            Logger.Log($"Tracer.SpanBuilderWithRemoteParent");
            return new LoggingSpanBuilder(spanName, spanKind, remoteParentSpanContext);
        }

        public IScope WithSpan(ISpan span)
        {
            Logger.Log($"Tracer.WithSpan");
            return new CurrentSpanUtils.LoggingScope(span);
        }
    }
}


