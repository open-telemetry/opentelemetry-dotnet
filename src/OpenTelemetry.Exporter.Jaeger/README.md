# Jaeger Exporter for OpenTelemetry .NET

The Jaeger exporter communicates to a Jaeger Agent through the compact thrift protocol on
the Compact Thrift API port. You can configure the Jaeger exporter by following the directions below:

1. [Get Jaeger][jaeger-get-started].
2. Configure the `JaegerExporter`
    - `ServiceName`: The name of your application or service.
    - `AgentHost`: Usually `localhost` since an agent should usually be running on the same machine as your application or service.
    - `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default `6831`)
    - `MaxPacketSize`: The maximum size of each UDP packet that gets sent to the agent. (default `65000`)
3. See the [sample][jaeger-sample] for an example of how to use the exporter.

```csharp
using (var tracerFactory = TracerFactory.Create(
    builder => builder.UseJaeger(o =>
    {
        o.ServiceName = "jaeger-test";
        o.AgentHost = "<jaeger server>";
    })))
{
    var tracer = tracerFactory.GetTracer("jaeger-test");
    using (tracer.StartActiveSpan("incoming request", out var span))
    {
        span.SetAttribute("custom-attribute", 55);
        await Task.Delay(1000);
    }
}
```

[OpenTelemetry-exporter-jaeger-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Jaeger.svg
[OpenTelemetry-exporter-jaeger-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Jaeger
[OpenTelemetry-exporter-jaeger-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Jaeger.svg
[OpenTelemetry-exporter-jaeger-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger
[jaeger-get-started]: https://www.jaegertracing.io/docs/1.13/getting-started/
[jaeger-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestJaeger.cs
