using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace OpenTelemetry.Exporter.ElasticApm
{
    internal class ElasticApmExporter : BaseExporter<Activity>
    {
        private readonly HttpClient httpClient;

        public ElasticApmExporter(ElasticApmOptions options, HttpClient httpClient = null)
        {
            this.httpClient = httpClient ?? this.CreateHttpClient(options);
            this.Options = options;
        }

        internal ElasticApmOptions Options { get; }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            using var scope = SuppressInstrumentationScope.Begin();

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    this.Options.IntakeApiVersion)
                {
                    Content = new NdjsonContent(this.Options, batch),
                };

                using var response = this.httpClient
                    .SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                response.EnsureSuccessStatusCode();

                return ExportResult.Success;
            }
            catch
            {
                return ExportResult.Failure;
            }
        }

        private HttpClient CreateHttpClient(ElasticApmOptions options)
        {
            return new HttpClient
            {
                BaseAddress = new UriBuilder(options.ServerScheme, options.ServerHost, options.ServerPort).Uri,
            };
        }
    }
}
