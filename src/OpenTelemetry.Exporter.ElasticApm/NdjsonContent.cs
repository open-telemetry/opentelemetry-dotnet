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
        private Utf8JsonWriter writer;

        public NdjsonContent(ElasticApmExporter exporter, in Batch<Activity> batch)
        {
            this.exporter = exporter;
            this.batch = batch;

            this.Headers.ContentType = NdjsonHeader;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            this.EnsureWriter(stream);

            this.writer.WriteStartObject(); // TODO: write metadata

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
    }
}
