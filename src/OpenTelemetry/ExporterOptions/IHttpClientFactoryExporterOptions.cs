using System;
using System.Net.Http;

namespace OpenTelemetry.Exporter
{
    public interface IHttpClientFactoryExporterOptions
    {
        /// <summary>
        /// Gets or sets the factory function called to create the <see
        /// cref="HttpClient"/> instance that will be used at runtime to
        /// transmit telemetry over HTTP. The returned instance will be reused
        /// for all export invocations.
        /// </summary>
        public Func<HttpClient> HttpClientFactory { get; set; }
    }
}
