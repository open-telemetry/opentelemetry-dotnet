using System;
using System.Net;
using System.Threading;

namespace OpenTelemetry.Exporter.Prometheus.HTTP.Server
{
    internal class PrometheusExporterHttpServer
    {
        private readonly HttpListener listener = new();
        private readonly object syncObject = new();

        private CancellationTokenSource tokenSource;

        public PrometheusExporterHttpServer(PrometheusExporterHttpServerOptions options)
        {
            if ((options.HttpListenerPrefixes?.Count ?? 0) <= 0)
            {
                throw new ArgumentException("No HttpListenerPrefixes were specified on PrometheusExporterHttpServerOptions.");
            }
        }

        public void Start()
        {

        }

    }
}
