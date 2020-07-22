# Customizing OpenTelemetry .NET

## Custom Samplers

You may configure sampler of your choice

```csharp
TracerProviderSdk.EnableTracerProvider(b => b
    .AddActivitySource(ActivitySourceName)
    .SetSampler(new ProbabilityActivitySampler(0.1))
    .SetResource(Resources.Resources.CreateServiceResource("my-service"))
    .UseZipkinExporter(options => {}));
```

You can also implement a custom sampler and should subclass `ActivitySampler`

```csharp
class MySampler : ActivitySampler
{
    public override string Description { get; } = "my custom sampler";

    public override SamplingResult ShouldSample(in ActivitySamplingParameters samplingParameters)
    {
        bool sampledIn;
        var parentContext = samplingParameters.ParentContext;
        if (parentContext != null && parentContext.IsValid())
        {
            sampledIn = (
                parentContext.TraceFlags & ActivityTraceFlags.Recorded
            ) != 0;
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

* Exporters should subclass `ActivityExporter` and implement `ExportAsync` and
  `ShutdownAsync` methods.
* Depending on user's choice and load on the application `ExportAsync` may get
  called concurrently with zero or more activities.
* Exporters should expect to receive only sampled-in ended activities.
* Exporters must not throw.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).

It's a good practice to make exporter `IDisposable` and shut it down in
IDispose unless it was shut down explicitly. This helps when exporters are
registered with dependency injection framework and their lifetime is tight to
the app lifetime.

```csharp
class MyExporter : ActivityExporter, IDisposable
{
    public override Task<ExportResult> ExportAsync(
        IEnumerable<Activity> batch, CancellationToken cancellationToken)
    {
        foreach (var activity in batch)
        {
            Console.WriteLine(
                $"[{activity.StartTimeUtc:o}] " +
                $"{activity.DisplayName} " +
                $"{activity.Context.TraceId.ToHexString()} " +
                $"{activity.Context.SpanId.ToHexString()}"
            );
        }

        return Task.FromResult(ExportResult.Success);
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // ...
    }

    protected virtual void Dispose(bool disposing)
    {
        // ...
    }
}
```

* Users may configure the exporter similarly to other exporters.
* You should also provide additional methods to simplify configuration
  similarly to `UseZipkinExporter` extension method.

```csharp
TracerProviderSdk.EnableTracerProvider(b => b
    .AddActivitySource(ActivitySourceName)
    .UseMyExporter();
```
