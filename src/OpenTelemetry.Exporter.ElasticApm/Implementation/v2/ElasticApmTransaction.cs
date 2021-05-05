using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation.V2
{
    internal readonly struct ElasticApmTransaction : IJsonSerializable
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
            writer.WriteStartObject();

            writer.WritePropertyName(ElasticApmJsonHelper.TransactionPropertyName);
            writer.WriteStartObject();

            writer.WriteString(ElasticApmJsonHelper.NamePropertyName, this.Name);
            writer.WriteString(ElasticApmJsonHelper.TraceIdPropertyName, this.TraceId);
            writer.WriteString(ElasticApmJsonHelper.IdPropertyName, this.Id);
            writer.WriteString(ElasticApmJsonHelper.ParentIdPropertyName, this.ParentId);
            writer.WriteNumber(ElasticApmJsonHelper.DurationPropertyName, this.Duration);
            writer.WriteNumber(ElasticApmJsonHelper.TimestampPropertyName, this.Timestamp);
            writer.WriteString(ElasticApmJsonHelper.TypePropertyName, this.Type);

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
