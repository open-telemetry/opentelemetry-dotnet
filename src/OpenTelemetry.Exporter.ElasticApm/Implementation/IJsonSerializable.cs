using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation
{
    internal interface IJsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    }
}
