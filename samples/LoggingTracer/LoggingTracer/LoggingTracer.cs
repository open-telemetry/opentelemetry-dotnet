namespace LoggingTracer
{
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracer : ITracer
    {
        private readonly string name;
        
        public LoggingTracer(string name)
        {
            this.name = name;
        }
        
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        public IBinaryFormat BinaryFormat => new LoggingBinaryFormat();

        public ITextFormat TextFormat => new LoggingTextFormat();

        public void RecordSpanData(SpanData span)
        {
            Logger.Log($"Tracer({name}).RecordSpanData({span})");
        }

        public ISpanBuilder SpanBuilder(string spanName)
        {
            Logger.Log($"Tracer({name}).SpanBuilder({spanName})");
            return new LoggingSpanBuilder(spanName, SpanKind.Internal);
        }

        public ISpanBuilder SpanBuilderWithParent(string spanName, SpanKind spanKind = SpanKind.Internal, ISpan parent = null)
        {
            Logger.Log($"Tracer({name}).SpanBuilderWithExplicitParent({spanName}, {spanKind}, {parent})");
            return new LoggingSpanBuilder(spanName, spanKind, parent);
        }

        public ISpanBuilder SpanBuilderWithParentContext(string spanName, SpanKind spanKind = SpanKind.Internal, SpanContext remoteParentSpanContext = null)
        {
            Logger.Log($"Tracer({name}).SpanBuilderWithRemoteParent");
            return new LoggingSpanBuilder(spanName, spanKind, remoteParentSpanContext);
        }

        public IScope WithSpan(ISpan span)
        {
            Logger.Log($"Tracer({name}).WithSpan");
            return new CurrentSpanUtils.LoggingScope(span);
        }
    }
}
