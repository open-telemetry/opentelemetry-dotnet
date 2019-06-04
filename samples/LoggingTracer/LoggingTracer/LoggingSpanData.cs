using OpenTelemetry.Common;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace LoggingTracer
{
    public class LoggingSpanData : ISpanData
    {
        public SpanContext Context { get; set; }

        public ISpanId ParentSpanId { get; set; }

        public bool? HasRemoteParent { get; set; }

        public string Name { get; set; }

        public Timestamp StartTimestamp { get; set; }

        public IAttributes Attributes { get; set; }

        public ITimedEvents<IEvent> Events { get; set; }

        public ILinks Links { get; set; }

        public int? ChildSpanCount { get; set; }

        public Status Status { get; set; }

        public SpanKind Kind { get; set; }

        public Timestamp EndTimestamp { get; set; }
    }
}


