# Extending the OpenTelemetry .NET SDK

* [Building your own exporter](#exporter)
* [Building your own processor](#processor)
* [Building your own sampler](#sampler)
* [Building your own resource detector](../../resources/README.md#resource-detector)
* [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in log exporters:

* [InMemory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)

Custom exporters can be implemented to send telemetry data to places which are
not covered by the built-in exporters:

* Exporters should derive from `OpenTelemetry.BaseExporter<LogRecord>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package)
  and implement the `Export` method.
* Exporters can optionally implement the `OnForceFlush` and `OnShutdown` method.
* Depending on user's choice and load on the application, `Export` may get
  called with one or more log records.
* Exporters should not throw exceptions from `Export`, `OnForceFlush` and
  `OnShutdown`.
* Exporters should not modify log records they receive (the same log records may
  be exported again by different exporter).
* Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.
* Exporters should avoid generating telemetry and causing live-loop, this can be
  done via `OpenTelemetry.SuppressInstrumentationScope`.

```csharp
class MyExporter : BaseExporter<LogRecord>
{
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();

        foreach (var record in batch)
        {
            Console.WriteLine($"Export: {record}");
        }

        return ExportResult.Success;
    }
}
```

A demo exporter which simply writes log records to the console is shown
[here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extension methods as
shown [here](./LoggerExtensions.cs). This allows users to add the Exporter to
the `OpenTelemetryLoggerOptions` as shown in the example [here](./Program.cs).

## Processor

OpenTelemetry .NET SDK has provided the following built-in processors:

* [BatchExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/BatchExportProcessor.cs)
* [CompositeProcessor&lt;T&gt;](../../../src/OpenTelemetry/CompositeProcessor.cs)
* [SimpleExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/SimpleExportProcessor.cs)

Custom processors can be implemented to cover more scenarios:

* Processors should inherit from `OpenTelemetry.BaseProcessor<LogRecord>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package),
  and implement the `OnEnd` method.
* Processors can optionally implement the `OnForceFlush` and `OnShutdown`
  methods. `OnForceFlush` should be thread safe.
* Processors should not throw exceptions from `OnEnd`, `OnForceFlush` and
  `OnShutdown`.
* `OnEnd` should be thread safe, and should not block or take long time, since
  they will be called on critical code path.

```csharp
class MyProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
        Console.WriteLine($"OnEnd: {record}");
    }
}
```

A demo processor is shown [here](./MyProcessor.cs).

### Filtering Processor

Another common use case of writing a custom processor is to filter log records
from being exported. Unlike `Activity`, `LogRecord` does not have a `Recorded`
flag which exporting processors check to skip telemetry, so a
"FilteringProcessor" can be written by deriving from
`BatchLogRecordExportProcessor` or `SimpleLogRecordExportProcessor` and only
calling `base.OnEnd` for the log records which should be exported. An example
filtering processor is shown [here](./MyFilteringProcessor.cs).

A filtering processor like this is registered in place of the export
processor it derives from:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.AddProcessor(new MyFilteringProcessor(new MyExporter(), logRecord => true));
    });
});
```

> [!NOTE]
> A filtering processor wraps an exporter directly, so it must be registered
> manually using `AddProcessor` (as shown above). The `Add*Exporter` extension
> methods shipped from this repo register their own exporting processor and
> cannot be combined with a filtering processor.

## Sampler

TBD

## References
