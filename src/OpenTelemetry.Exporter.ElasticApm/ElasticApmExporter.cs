using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace OpenTelemetry.Exporter.ElasticApm
{
    internal class ElasticApmExporter : BaseExporter<Activity>
    {
        private readonly ElasticApmOptions options;
        private readonly string requestUri;
        private readonly HttpClient httpClient;

        public ElasticApmExporter(ElasticApmOptions options, HttpClient httpClient = null)
        {
            this.options = options;
            this.httpClient = httpClient ?? CreateHttpClient(options);
            this.requestUri = this.GetIntakeApiUri();
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            using var scope = SuppressInstrumentationScope.Begin();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, this.requestUri)
                {
                    Content = null // TODO
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

        private static HttpClient CreateHttpClient(ElasticApmOptions options)
        {
            return new HttpClient
            {
                BaseAddress = new UriBuilder(options.ServerScheme, options.ServerHost, options.ServerPort).Uri,
            };
        }

        private string GetIntakeApiUri()
        {
            return this.options.IntakeApiVersion == IntakeApiVersion.V2
                ? "/intake/v2/events"
                : throw new NotSupportedException();
        }
    }
}
