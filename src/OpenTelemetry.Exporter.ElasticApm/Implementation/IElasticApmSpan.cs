using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation
{
    internal interface IElasticApmSpan
    {
        void Write(Utf8JsonWriter writer);

        void Return();
    }
}
