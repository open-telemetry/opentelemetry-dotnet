# Building your own Exporter

* To export telemetry to a specific destination, custom exporters must be
  written.
* Exporters should inherit from `ActivityExporter` and implement `Export` and
  `Shutdown` methods. `ActivityExporter` is part of the [OpenTelemetry
  Package](https://www.nuget.org/packages/opentelemetry).
* Depending on user's choice and load on the application, `Export` may get
  called with zero or more activities.
* Exporters will only receive sampled-in and ended activities.
* Exporters must not throw.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).
* Any retry logic that is required by the exporter is the responsibility of the
  exporter, as the SDK does not implement retry logic.

## Example

A sample exporter, which simply writes activity name to the console is shown
[here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extension methods as
shown [here](./MyExporterHelperExtensions.cs). This allows users to add the
Exporter to the `TracerProvider` as shown in the sample code
[here](./Program.cs).

To run the full example code demonstrating the exporter, run the following
command from this folder.

```sh
dotnet run
```

## References

* [Exporter specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-exporter)
* Exporters provided by this repository.
  * [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
  * [Jaeger](../../../src/OpenTelemetry.Exporter.Jaeger/README.md)
  * [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
  * [Zipkin](../../../src/OpenTelemetry.Exporter.Zipkin/README.md)
