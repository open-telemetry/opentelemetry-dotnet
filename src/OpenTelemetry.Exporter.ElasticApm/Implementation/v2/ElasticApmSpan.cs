using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation.V2
{
    internal readonly struct ElasticApmSpan : IElasticApmSpan
    {
        public void Write(Utf8JsonWriter writer)
        {
        }

        public void Return()
        {
        }
    }
}
