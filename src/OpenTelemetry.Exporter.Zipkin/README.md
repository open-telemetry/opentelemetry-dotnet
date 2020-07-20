# Zipkin Exporter for OpenTelemetry .NET

Configure Zipkin exporter to see traces in Zipkin UI.

1. Get Zipkin using [getting started
   guide](https://zipkin.io/pages/quickstart.html).
2. Configure `ZipkinTraceExporter` as below:
3. See
   [sample](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestZipkin.cs)
   for example use.

```csharp
using (var tracerFactory = TracerFactory.Create(builder => builder
    .UseZipkin(o =>
    {
        o.ServiceName = "test-zipkin";
        o.Endpoint = new Uri(zipkinUri);
    })))
{
    var tracer = tracerFactory.GetTracer("zipkin-test");

    // Create a scoped span. It will end automatically when using statement ends
    using (tracer.WithSpan(tracer.StartSpan("Main")))
    {
        Console.WriteLine("About to do a busy work");
        for (var i = 0; i < 10; i++)
        {
            DoWork(i, tracer);
        }
    }
}
```
