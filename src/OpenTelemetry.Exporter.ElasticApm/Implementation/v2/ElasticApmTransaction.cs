using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation.V2
{
    internal readonly struct ElasticApmTransaction : IElasticApmSpan
    {
        public ElasticApmTransaction(
            string name,
            string traceId,
            string id,
            string parentId,
            long duration,
            long timestamp,
            string type)
        {
            this.Id = id;
            this.ParentId = parentId;
            this.TraceId = traceId;
            this.Name = name;
            this.Duration = duration;
            this.Timestamp = timestamp;
            this.Type = type;
        }

        public string Id { get; }

        public string TraceId { get; }

        public string ParentId { get; }

        public string Name { get; }

        public long Duration { get; }

        public long Timestamp { get; }

        public string Type { get; }

        public void Write(Utf8JsonWriter writer)
        {
        }

        public void Return()
        {
        }
    }
}
