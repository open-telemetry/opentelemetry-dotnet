using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation.V2
{
    internal readonly struct ElasticApmMetadata : IJsonSerializable
    {
        public ElasticApmMetadata(Service service)
        {
            this.Service = service;
        }

        internal Service Service { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(ElasticApmJsonHelper.MetadataPropertyName);
            writer.WriteStartObject();

            writer.WritePropertyName(ElasticApmJsonHelper.ServicePropertyName);
            this.Service.Write(writer);

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }

    internal readonly struct Service
    {
        public Service(string name, string environment, Agent agent)
        {
            this.Name = name;
            this.Environment = environment;
            this.Agent = agent;
        }

        internal string Name { get; }

        internal string Environment { get; }

        internal Agent Agent { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WriteString(ElasticApmJsonHelper.NamePropertyName, this.Name);
            writer.WriteString(ElasticApmJsonHelper.EnvironmentPropertyName, this.Environment);
            writer.WritePropertyName(ElasticApmJsonHelper.AgentPropertyName);
            this.Agent.Write(writer);

            writer.WriteEndObject();
        }
    }

    internal readonly struct Agent
    {
        public Agent(Assembly assembly)
        {
            this.Name = "opentelemetry";
            this.Version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        }

        internal string Name { get; }

        internal string Version { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WriteString(ElasticApmJsonHelper.NamePropertyName, this.Name);
            writer.WriteString(ElasticApmJsonHelper.VersionPropertyName, this.Version);

            writer.WriteEndObject();
        }
    }
}
