# Threat model

## What this project does and where untrusted input enters

- This project is the .NET SDK for OpenTelemetry - it produces logs, metrics and
  traces for .NET libraries and applications.
- Untrusted input enters through the data that the SDK receives from the
  applications and libraries it instruments. This data can come through either
  user-provided data to attributes or can come through untrusted input in the
  case of HTTP requests if used for baggage and trace propagation.

## Components that matter most / least

- The following components matter most:
  - OpenTelemetry.Api - contains the core primitives and context propagation mechanisms.
  - OpenTelemetry.Api.ProviderBuilderExtensions - contains extension methods for
    configuring and building OpenTelemetry providers.
  - OpenTelemetry - contains the main implementation of the OpenTelemetry SDK,
    including the core tracing, metrics, and logging functionality.
  - OpenTelemetry.Extensions.Hosting - contains extension methods and helpers for
    integrating OpenTelemetry with .NET hosting infrastructure, such as ASP.NET
    Core applications.
  - OpenTelemetry.Exporter.OpenTelemetryProtocol - contains the implementation of
    the OpenTelemetry Protocol (OTLP) exporter for sending telemetry data to a
    local or remote OTLP collector using either gRPC or HTTP/protobuf.
  - OpenTelemetry.Exporter.Prometheus.AspNetCore - contains the implementation of
    the Prometheus exporter for ASP.NET Core applications.
  - OpenTelemetry.Exporter.Prometheus.HttpListener - contains the implementation
    of the Prometheus exporter for applications using HttpListener.
  - OpenTelemetry.Extensions.Propagators - contains implementations of various
    context propagation mechanisms, such as B3, W3C Trace Context, and Jaeger,
    for distributing trace context across service boundaries.
  - OpenTelemetry.Configuration.Declarative - contains the implementation of
    declarative configuration for OpenTelemetry, allowing users to configure the
    SDK through configuration files or environment variables.
- The following components matter least as they are not designed for production
  use:
  - OpenTelemetry.Exporter.Console - contains the implementation of a console
    exporter, primarily used for local development.
  - OpenTelemetry.Exporter.InMemory - contains the implementation of an
    in-memory exporter used for testing.
  - OpenTelemetry.Exporter.Zipkin - contains the implementation of the Zipkin
    exporter, which is on the path to being deprecated.
  - OpenTelemetry.Shims.OpenTracing - contains shims for compatibility with
    older versions of OpenTracing APIs, which is on the path to being deprecated.

## How to exercise it

- The examples directory contains example usage applications.
- The test directory contains all of the unit and integration tests, including
  fuzz tests.

## How you rate severity

No special rules for rating severity are provided; use standard security severity
guidelines.

## Anything to leave alone

- Ignore any projects that are deprecated or intended only for development or testing
  purposes unless they are high or critical severity.
