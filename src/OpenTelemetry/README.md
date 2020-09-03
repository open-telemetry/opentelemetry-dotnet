# OpenTelemetry .NET SDK

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)

* [Installation](#installation)
* [Introduction](#introduction)
* [Getting started](#getting-started)
* [Configuration](#configuration)
  * [Instrumentation](#instrumentation)
  * [Processor](#processor)
  * [Resource](#resource)
  * [Sampler](#sampler)
* [Advanced topics](#advanced-topics)
* [References](#references)

## Installation

```shell
dotnet add package OpenTelemetry
```

## Introduction

OpenTelemetry SDK is a reference implementation of the OpenTelemetry API. It
implements the Tracing API, the Metrics API, and the Context API. OpenTelemetry
SDK deals with concerns such as sampling, processing pipeline, exporting
telemetry to a particular backend etc. The default implementation consists of
the following.

* Set of [Built-in
  samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-samplers)
* Set of [Built-in
  processors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-span-processors).
  * SimpleProcessor which sends Activities to the exporter without any
    batching.
  * BatchingProcessor which batches and sends Activities to the exporter.
* Extensibility options for users to customize SDK.

## Getting started

Please follow the tutorial and [get started in 5
minutes](../../docs/trace/getting-started/README.md).

## Configuration

### Instrumentation

### Processor

### Resource

### Sampler

[Samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#sampler)
are used to control the noise and overhead introduced by OpenTelemetry by
reducing the number of samples of traces collected and sent to the backend. If
no sampler is explicitly specified, the default is to use
[AlwaysOnSampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#alwayson).
The following example shows how to change it to
[TraceIdRatioBasedSampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#traceidratiobased)
with sampling probability of 25%.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var otel = Sdk.CreateTracerProvider(b => b
    .AddActivitySource("MyCompany.MyProduct.MyLibrary")
    .SetSampler(new TraceIdRatioBasedSampler(0.25))
    .UseConsoleExporter());
```

## Advanced topics

* Logs
  * [Correlating logs with traces](../../docs/logs/correlation/README.md)
* Metrics
  * [Building your own Exporter](../../docs/metrics/building-your-own-exporter.md)
* Trace
  * [Building your own Exporter](../../docs/trace/extending-the-sdk/README.md#exporter)
  * [Building your own Instrumentation
    Library](../../docs/trace/extending-the-sdk/README.md#instrumentation-library)
  * [Building your own Processor](../../docs/trace/extending-the-sdk/README.md#processor)
  * [Building your own Sampler](../../docs/trace/extending-the-sdk/README.md#sampler)

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
