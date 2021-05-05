using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.ElasticApm.Implementation;

namespace OpenTelemetry.Exporter.ElasticApm
{
    internal class NdjsonContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue NdjsonHeader =
            new MediaTypeHeaderValue("application/x-ndjson")
            {
                CharSet = new UTF8Encoding(false).WebName,
            };

        private readonly ElasticApmExporter exporter;
        private readonly Batch<Activity> batch;
        private readonly IJsonSerializable metadata;
        private Utf8JsonWriter writer;

        public NdjsonContent(ElasticApmExporter exporter, in Batch<Activity> batch)
        {
            this.exporter = exporter;
            this.batch = batch;
            this.metadata = this.CreateMetadata();

            this.Headers.ContentType = NdjsonHeader;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            this.EnsureWriter(stream);

            this.metadata.Write(this.writer);
            this.metadata.Return();

            foreach (var activity in this.batch)
            {
                var span = activity.ToElasticApmSpan(this.exporter.Options.IntakeApiVersion);
                span.Write(this.writer);
                span.Return();

                if (this.writer.BytesPending >= this.exporter.MaxPayloadSizeInBytes)
                {
                    this.writer.Flush();
                }
            }

            this.writer.Flush();

            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        private void EnsureWriter(Stream stream)
        {
            if (this.writer == null)
            {
                this.writer = new Utf8JsonWriter(stream);
            }
            else
            {
                this.writer.Reset(stream);
            }
        }

        private IJsonSerializable CreateMetadata()
        {
            return new Implementation.V2.ElasticApmMetadata(
                new Implementation.V2.Service(
                    this.exporter.Options.Name,
                    this.exporter.Options.Environment,
                    new Implementation.V2.Agent(typeof(ElasticApmExporter).Assembly)));
        }
    }
}
