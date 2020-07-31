# Building your own Exporter

* Exporters should inherit from `ActivityExporter` and implement `ExportAsync`
  and `ShutdownAsync` methods.
* Depending on user's choice and load on the application `ExportAsync` may get
  called concurrently with zero or more activities.
* Exporters should expect to receive only sampled-in ended activities.
* Exporters must not throw.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).

```csharp
class MyExporter : ActivityExporter
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

    protected override void Dispose(bool disposing)
    {
        // flush the data and clean up the resource
    }
}
```

* Users may configure the exporter similarly to other exporters.
* You should also provide additional methods to simplify configuration
  similarly to `UseZipkinExporter` extension method.

```csharp
Sdk.CreateTracerProvider(b => b
    .AddActivitySource(ActivitySourceName)
    .UseMyExporter();
```
