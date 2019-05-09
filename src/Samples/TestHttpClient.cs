namespace Samples
{
    using System;
    using System.Net.Http;
    using OpenCensus.Collector.Dependencies;
    using OpenCensus.Exporter.Zipkin;
    using OpenCensus.Trace;
    using OpenCensus.Trace.Propagation;
    using OpenCensus.Trace.Sampler;

    internal class TestHttpClient
    {
        private static ITracer tracer = Tracing.Tracer;

        internal static object Run()
        {
            Console.WriteLine("Hello World!");

            var collector = new DependenciesCollector(new DependenciesCollectorOptions(), tracer, Samplers.AlwaysSample, PropagationComponentBase.NoopPropagationComponent);

            var exporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions()
                {
                    Endpoint = new Uri("https://zipkin.azurewebsites.net/api/v2/spans"),
                    ServiceName = typeof(Program).Assembly.GetName().Name,
                },
                Tracing.ExportComponent);
            exporter.Start();

            var scope = tracer.SpanBuilder("incoming request").SetSampler(Samplers.AlwaysSample).StartScopedSpan();
            //Thread.Sleep(TimeSpan.FromSeconds(1));

            HttpClient client = new HttpClient();
            var t = client.GetStringAsync("http://bing.com");

            t.Wait();

            scope.Dispose();

            Console.ReadLine();

            return null;
        }
    }
}
