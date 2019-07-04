namespace Samples
{
    using System;
    using System.Net.Http;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Exporter.Zipkin;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal class TestHttpClient
    {
        private static readonly ITracer tracer = Tracing.Tracer;

        internal static object Run()
        {
            Console.WriteLine("Hello World!");

            using (new DependenciesCollector(new DependenciesCollectorOptions(), tracer, Samplers.AlwaysSample))
            {

                var exporter = new ZipkinTraceExporter(
                    new ZipkinTraceExporterOptions()
                    {
                        Endpoint = new Uri("https://zipkin.azurewebsites.net/api/v2/spans"),
                        ServiceName = typeof(Program).Assembly.GetName().Name,
                    },
                    Tracing.ExportComponent);
                exporter.Start();

                using (tracer.WithSpan(tracer.SpanBuilder("incoming request").SetSampler(Samplers.AlwaysSample).StartSpan()))
                {
                    using (var client = new HttpClient())
                    {
                        client.GetStringAsync("http://bing.com").GetAwaiter().GetResult();
                    }
                }

                Console.ReadLine();

                return null;
            }
        }
    }
}
