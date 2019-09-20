using System;

namespace LoggingTracer
{
    using System.Collections.Generic;
    using OpenTelemetry.Trace;

    public class LoggingSpan : ISpan
    {
        public LoggingSpan(string name, SpanKind kind)
        {
            Logger.Log($"Span.ctor({name}, {kind})");
            this.Name = name;
            this.Kind = kind;
        }

        public string Name { get; set; }

        public SpanContext Context { get; set; }

        public Status Status { get; set; }

        public SpanKind? Kind { get; set; }

        public bool HasEnded { get; set; }

        public bool IsRecordingEvents => true;

        public void AddEvent(string name) => Logger.Log($"Span.AddEvent({name})");

        public void AddEvent(string name, IDictionary<string, object> attributes)
            => Logger.Log($"Span.AddEvent({name}, attributes: {attributes.Count})");

        public void AddEvent(IEvent newEvent) => Logger.Log($"Span.AddEvent({newEvent})");

        public void AddLink(ILink link) => Logger.Log($"Span.AddLink({link})");

        public void End() => Logger.Log($"Span.End, Name: {this.Name}");

        public void End(DateTime endTimestamp) => Logger.Log($"Span.End, Name: {this.Name}, Timestamp: {endTimestamp}");

        public void SetAttribute(string key, object value) => this.LogSetAttribute(key, value);

        public void SetAttribute(string key, string value) => this.LogSetAttribute(key, value);

        public void SetAttribute(string key, long value) => this.LogSetAttribute(key, value);

        public void SetAttribute(string key, double value) => this.LogSetAttribute(key, value);

        public void SetAttribute(string key, bool value) => this.LogSetAttribute(key, value);

        public void SetAttribute(KeyValuePair<string, object> keyValuePair)
        {
            Logger.Log($"Span.SetAttributes(attributes: {keyValuePair})");
            this.SetAttribute(keyValuePair.Key, keyValuePair.Value);
        }

        public void UpdateName(string name)
        {
            Logger.Log($"Span.UpdateName({name})");
            this.Name = name;
        }

        private void LogSetAttribute(string key, object value) => Logger.Log($"Span.SetAttribute({key}, {value})");
    }
}
