using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation.V2
{
    internal readonly struct ElasticApmSpan : IElasticApmSpan
    {
        public ElasticApmSpan(string name)
        {
            this.Name = name;
        }

        public string Name { get; }

        public void Write(Utf8JsonWriter writer)
        {
        }

        public void Return()
        {
        }
    }
}
