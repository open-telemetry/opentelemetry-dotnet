using System.Collections.Generic;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingSpan : ISpan
    {
        public string Name { get; set; }

        public SpanContext Context { get; set; }

        public Status Status { get; set; }

        public SpanKind? Kind { get; set; }

        public bool HasEnded { get; set; }

        public bool IsRecordingEvents => throw new System.NotImplementedException();

        public LoggingSpan(string name, SpanKind kind)
        {
            Logger.Log($"Span.ctor({name})");
            this.Name = name;
            this.Kind = kind;
        }

        public void AddEvent(string name)
        {
            Logger.Log($"Span.AddEvent");
        }

        public void AddEvent(string name, IDictionary<string, IAttributeValue> attributes)
        {
            Logger.Log($"Span.AddEvent");
        }

        public void AddEvent(IEvent newEvent)
        {
            Logger.Log($"Span.AddEvent");
        }

        public void AddLink(ILink link)
        {
            Logger.Log($"Span.AddLink");
        }

        public void End()
        {
            Logger.Log($"Span.End({Name})");
        }

        public void SetAttribute(string key, IAttributeValue value)
        {
            Logger.Log($"Span.SetAttribute({key}={value})");
        }

        public void SetAttribute(string key, string value)
        {
            Logger.Log($"Span.SetAttribute({key}={value})");
        }

        public void SetAttribute(string key, long value)
        {
            Logger.Log($"Span.SetAttribute({key}={value})");
        }

        public void SetAttribute(string key, double value)
        {
            Logger.Log($"Span.SetAttribute({key}={value})");
        }

        public void SetAttribute(string key, bool value)
        {
            Logger.Log($"Span.SetAttribute({key}={value})");
        }

        public void SetAttributes(IDictionary<string, IAttributeValue> attributes)
        {
            Logger.Log($"Span.SetAttributes");
            foreach (var attribute in attributes)
            {
                SetAttribute(attribute.Key, attribute.Value);
            }
        }

        public void UpdateName(string name)
        {
            Logger.Log($"Span.UpdateName({name})");
        }
    }
}
