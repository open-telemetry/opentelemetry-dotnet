# Building your own Exporter

## Background

* [ActivityExporter](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-exporter)
  export activities to specific destination.
* OpenTelemetry .NET repo provides official exporters for
  [Console](../../../src/OpenTelemetry.Exporter.Console/README.md),
  [Jaeger](../../../src/OpenTelemetry.Exporter.Jaeger/README.md),
  [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  and [Zipkin](../../../src/OpenTelemetry.Exporter.Zipkin/README.md).

## Building own Exporter

* To export telemetry to a specific destination, custom exporters must be
  written.
* Exporters should inherit from `ActivityExporter` and implement `ExportAsync`
  and `ShutdownAsync` methods. `ActivityExporter` is part of the [OpenTelemetry
  Package](https://www.nuget.org/packages/opentelemetry).
* Depending on user's choice and load on the application, `ExportAsync` may get
  called with zero or more activities.
* Exporters should expect to receive only sampled-in and ended activities.
* Exporters must not throw.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).
* Exporters are expected to handle failures and destination appropriate retry
  logic.

## Example

A sample exporter, which simply writes activity name
to the console is shown [here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extensions methods to
simplify adding the exporter to the `TracerProvider` as shown
[here](./MyExporterHelperExtensions.cs). This allows users to add the Exporter to
the `TracerProvider` as shown below.  

```csharp
Sdk.CreateTracerProvider(b => b
    .AddActivitySource(ActivitySourceName)
    .AddMyExporter();
```

