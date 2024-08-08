# OpenTelemetry Protocol (OTLP)

The [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification)
defines the [OpenTelemetry Protocol
(OTLP)](https://github.com/open-telemetry/opentelemetry-proto/tree/main/docs)
which is a standard for exporting telemetry in a vendor agnostic way.
OpenTelemetry .NET ships
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
which is an implementation of OTLP and may be used to export telemetry to any
backend with OTLP support or to the [OpenTelemetry
Collector](https://github.com/open-telemetry/opentelemetry-collector).

## Getting started

If you are new to the [OpenTelemetry Protocol
(OTLP)](https://github.com/open-telemetry/opentelemetry-proto/tree/main/docs),
it is recommended to first follow the [getting started in 5 minutes - ASP.NET
Core Application](./getting-started-aspnetcore/README.md) guide or the [getting
started in 5 minutes - Console Application](./getting-started-console/README.md)
guide to get up and running.

## Vendor support

* [NewRelic](https://newrelic.com/): [Getting Started Guide -
  .NET](https://github.com/newrelic/newrelic-opentelemetry-examples/tree/main/getting-started-guides/dotnet)
