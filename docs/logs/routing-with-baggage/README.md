# Routing Logs to Different Destinations Based on Baggage

This example shows how to route logs from a **single `ILogger`** to different
OTLP endpoints based on a
[Baggage](https://opentelemetry.io/docs/concepts/signals/baggage/) entry. This
follows the [Routing](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/supplementary-guidelines.md#routing)
pattern described in the OpenTelemetry specification supplementary guidelines.

## Overview

In some scenarios you need all application code to use the same `ILogger`, yet
send certain logs to one backend and the rest to another. For example:

* Logs produced by the **payments** team should go to a dedicated collector
  endpoint (`OTLP2`).
* All other logs should go to the default endpoint (`OTLP1`).

The routing decision is made at processor level by inspecting the current
`Baggage`. Because `Baggage` is propagated through the call chain, upstream
services (or middleware) can set a baggage entry and all downstream log
statements will be routed accordingly — without changing any logging code.

## Architecture

```text
ILogger (single instance)
   │
   ▼
LoggerProvider
   │
   ▼
BaggageRoutingProcessor (custom)
   ├── Baggage["team"] == "payments" ──► ExportProcessor → OtlpLogExporter (OTLP2)
   └── otherwise ──────────────────────► ExportProcessor → OtlpLogExporter (OTLP1)
```

## How it works

1. Two `OtlpLogExporter` instances are created, each pointing at a different
   endpoint.
2. Each exporter is wrapped in a `SimpleLogRecordExportProcessor` (use
   `BatchLogRecordExportProcessor` for production workloads).
3. A custom [`BaggageRoutingProcessor`](./BaggageRoutingProcessor.cs) extends
   `BaseProcessor<LogRecord>` and overrides `OnEnd`. It reads
   `Baggage.GetBaggage(key)` to decide which inner processor receives the log
   record.
4. The routing processor is registered on the `LoggerProvider` via
   `AddProcessor`.

## Running the example

```sh
cd docs/logs/routing-with-baggage
dotnet run
```

> [!NOTE]
> The example targets `http://localhost:4317` and `http://localhost:4318` as the
> two OTLP endpoints. You can start two local collectors (or use the [OpenTelemetry
> Collector](https://opentelemetry.io/docs/collector/)) listening on those ports
> to observe the routing behavior. The console exporter is also enabled so you
> can see all logs locally regardless of the OTLP endpoints.

## Key considerations

* **Baggage is ambient context.** The routing decision uses the `Baggage` that
  is current at the time the log is emitted. Make sure the baggage entry is set
  before the log statement executes.
* **Lifecycle management.** The routing processor delegates `ForceFlush`,
  `Shutdown`, and `Dispose` to both inner processors so that both export
  pipelines are properly drained and cleaned up.
* **Production batching.** Replace `SimpleLogRecordExportProcessor` with
  `BatchLogRecordExportProcessor` for production scenarios to get better
  throughput.
* **Extensibility.** The same pattern can route based on any condition
  available at log-emit time: log category name, severity, attributes, or other
  context values.

## Files

| File | Description |
|------|-------------|
| [Program.cs](./Program.cs) | Console app demonstrating the routing |
| [BaggageRoutingProcessor.cs](./BaggageRoutingProcessor.cs) | Custom routing processor |
| [routing-with-baggage.csproj](./routing-with-baggage.csproj) | Project file |
