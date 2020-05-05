# LightStep Exporter for OpenTelemetry .NET

Configure LightStep exporter to see traces in [LightStep](https://lightstep.com/).

1. Setup LightStep using [getting started](https://docs.lightstep.com/docs/welcome-to-lightstep) guide
2. Configure `LightStepTraceExporter` (see below)
3. See [sample](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestLightstep.cs) for example use

```csharp
using (var tracerFactory = TracerFactory.Create(
    builder => builder.UseLightStep(o =>
        {
            o.AccessToken = "<access-token>";
            o.ServiceName = "lightstep-test";
        })))
{
    var tracer = tracerFactory.GetTracer("lightstep-test");
    using (tracer.StartActiveSpan("incoming request", out var span))
    {
        span.SetAttribute("custom-attribute", 55);
        await Task.Delay(1000);
    }
}
```
