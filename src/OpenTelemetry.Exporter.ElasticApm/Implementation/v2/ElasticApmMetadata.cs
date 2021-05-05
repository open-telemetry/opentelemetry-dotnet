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
        }

        public void Return()
        {
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

        internal string Environment { get; }

        internal Agent Agent { get; }

        internal string Name { get; }
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
    }
}
