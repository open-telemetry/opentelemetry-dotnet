# Routing Logs to Different Destinations

This example shows how to route logs from a **single `ILogger`** to different
OTLP endpoints using a custom processor. This follows the
[Routing](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/supplementary-guidelines.md#routing)
pattern described in the OpenTelemetry specification supplementary guidelines.

## Overview

In some scenarios you need all application code to use the same `ILogger`
pipeline, yet send certain logs to one backend and the rest to another. For
example:

* Logs from **payment** components should go to a dedicated collector endpoint
  (`OTLP2`).
* All other logs should go to the default endpoint (`OTLP1`).

The routing decision is made at the processor level by inspecting the
`CategoryName` of each `LogRecord`. A custom processor checks whether the
category name starts with a configured prefix and forwards the record to the
appropriate export pipeline.

## Architecture

```text
ILogger (single pipeline)
   |
   v
LoggerProvider
   |
   v
RoutingProcessor (custom)
   +-- CategoryName starts with prefix --> ExportProcessor -> OtlpLogExporter (OTLP2)
   +-- otherwise --------------------------> ExportProcessor -> OtlpLogExporter (OTLP1)
```

## How it works

1. Two `OtlpLogExporter` instances are created, each pointing at a different
   endpoint.
2. Each exporter is wrapped in a `SimpleLogRecordExportProcessor` (use
   `BatchLogRecordExportProcessor` for production workloads).
3. A custom [`RoutingProcessor`](./RoutingProcessor.cs) extends
   `BaseProcessor<LogRecord>` and overrides `OnEnd`. It checks if the log
   record's `CategoryName` starts with a configured prefix to decide which
   inner processor receives the record.
4. The routing processor is registered on the `LoggerProvider` via
   `AddProcessor`.

## Running the example

```sh
cd docs/logs/routing
dotnet run
```

> [!NOTE]
> The example targets `http://localhost:4317` and `http://localhost:4318` as the
> two OTLP endpoints. You can start two local collectors (or use the
> [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)) listening
> on those ports to observe the routing behavior. The console exporter is also
> enabled so you can see all logs locally regardless of the OTLP endpoints.

## Key considerations

* **Routing condition is evaluated per log record.** Keep the logic fast --
  it runs synchronously on every log emit.
* **Lifecycle management.** The routing processor delegates `ForceFlush`,
  `Shutdown`, and `Dispose` to both inner processors so that both export
  pipelines are properly drained and cleaned up.
* **Production batching.** Replace `SimpleLogRecordExportProcessor` with
  `BatchLogRecordExportProcessor` for production scenarios to get better
  throughput.

## Files

| File | Description |
| ------ | ------------- |
| [Program.cs](./Program.cs) | Console app demonstrating the routing |
| [RoutingProcessor.cs](./RoutingProcessor.cs) | Custom routing processor |
| [routing.csproj](./routing.csproj) | Project file |
