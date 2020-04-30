# Customizing OpenTelemetry .NET

## Custom Samplers

You may configure sampler of your choice

```csharp
 using (TracerFactory.Create(b => b
            .SetSampler(new ProbabilitySampler(0.1))
            .UseZipkin(options => {})
            .SetResource(Resources.CreateServiceResource("my-service")))
{

}
```

You can also implement custom sampler by implementing `ISampler` interface

```csharp
class MySampler : Sampler
{
    public override string Description { get; } = "my custom sampler";

    public override Decision ShouldSample(SpanContext parentContext, ActivityTraceId traceId, ActivitySpanId spanId, string name,
        IDictionary<string, object> attributes, IEnumerable<Link> links)
    {
        bool sampledIn;
        if (parentContext != null && parentContext.IsValid)
        {
            sampledIn = (parentContext.TraceOptions & ActivityTraceFlags.Recorded) != 0;
        }
        else
        {
            sampledIn = Stopwatch.GetTimestamp() % 2 == 0;
        }

        return new Decision(sampledIn);
    }
}
```

## Custom Exporters

### Tracing

Exporters should subclass `SpanExporter` and implement `ExportAsync` and `Shutdown` methods.
Depending on user's choice and load on the application `ExportAsync` may get called concurrently with zero or more spans.
Exporters should expect to receive only sampled-in ended spans. Exporters must not throw. Exporters should not modify spans they receive (the same span may be exported again by different exporter).

It's a good practice to make exporter `IDisposable` and shut it down in IDispose unless it was shut down explicitly. This helps when exporters are registered with dependency injection framework and their lifetime is tight to the app lifetime.

```csharp
class MyExporter : SpanExporter
{
    public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
    {
        foreach (var span in batch)
        {
            Console.WriteLine($"[{span.StartTimestamp:o}] {span.Name} {span.Context.TraceId.ToHexString()} {span.Context.SpanId.ToHexString()}");
        }

        return Task.FromResult(ExportResult.Success);
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Users may configure the exporter similarly to other exporters.
You should also provide additional methods to simplify configuration similarly to `UseZipkin` extension method.

```csharp
var exporter = new MyExporter();
using (var tracerFactory = TracerFactory.Create(
    builder => builder.AddProcessorPipeline(b => b.SetExporter(new MyExporter())))
{
    // ...
}
```
