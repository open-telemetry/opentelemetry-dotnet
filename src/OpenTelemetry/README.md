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
  * [Building your own Exporter](#building-your-own-exporter)
  * [Building your own Sampler](#building-your-own-sampler)
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
minutes](../../docs/getting-started.md).

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
The following sample shows how to change it to
[ProbabilitySampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#probability)
with sampling probability of 25%.

```csharp
using OpenTelemetry.Trace.Samplers;

using var otel = OpenTelemetrySdk.CreateTracerProvider(b => b
    .AddActivitySource("MyCompany.MyProduct.MyLibrary")
    .SetSampler(new ProbabilitySampler(0.25))
    .UseConsoleExporter());
```

## Advanced topics

### Building your own Exporter

#### Trace Exporter

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
OpenTelemetrySdk.CreateTracerProvider(b => b
    .AddActivitySource(ActivitySourceName)
    .UseMyExporter();
```

### Building your own Sampler

* Samplers should inherit from `Sampler`, and implement `ShouldSample`
  method.
* `ShouldSample` should not block or take long time, since it will be called on
  critical code path.

```csharp
class MySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var shouldSample = true;

        return new SamplingResult(shouldSample);
    }
}
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
