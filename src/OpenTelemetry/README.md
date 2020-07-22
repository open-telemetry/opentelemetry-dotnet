# OpenTelemetry .NET SDK

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)

* [Installation](#installation)
* [Introduction](#introduction)
* [Basic usage](#basic-usage)
* [Advanced usage scenarios](#advanced-usage-scenarios)
  * [Customize Exporter](#customize-exporter)
  * [Customize Sampler](#customize-sampler)
  * [Customize Resource](#customize-resource)
  * [Filtering and enriching activities using
    Processor](#filtering-and-enriching-activities-using-processor)
  * [OpenTelemetry Instrumentation](#opentelemetry-instrumentation)
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
  * SimpleProcessor which sends Activities to the exporter without any batching.
  * BatchingProcessor which batches and sends Activities to the exporter.
* Extensibility options for users to customize SDK.

## Basic usage

The following examples show how to start collecting OpenTelemetry traces from a
console application, and have the traces displayed in the console.

1. Create a console application and install the `OpenTelemetry.Exporter.Console`
   package to your project.

    ```xml
    <ItemGroup>
      <PackageReference
        Include="OpenTelemetry.Exporter.Console"
        Version="0.3.0"
      />
    </ItemGroup>
    ```

2. At the beginning of the application, enable OpenTelemetry SDK with
   ConsoleExporter as shown below. It also configures to collect activities from
   the source named "companyname.product.library".

    ```csharp
    using var openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(builder => builder
                    .AddActivitySource("companyname.product.library")
                    .UseConsoleExporter())
    ```

    The above requires import of namespace `OpenTelemetry.Trace.Configuration`.

3. Generate some activities in the application as shown below.

    ```csharp
    var activitySource = new ActivitySource("companyname.product.library");

    using (var activity = activitySource.StartActivity("ActivityName", ActivityKind.Server))
    {
        activity?.AddTag("http.method", "GET");
    }
    ```

Run the application. Traces will be displayed in the console.

## Advanced usage scenarios

### Customize Exporter

### Customize Sampler

[Samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#sampler)
are used to control the noise and overhead introduced by OpenTelemetry by
reducing the number of samples of traces collected and sent to the backend. If
no sampler is explicitly specified, the default is to use
[AlwaysOnActivitySampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#alwayson).
The following sample shows how to change it to
[ProbabilityActivitySampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#probability)
with sampling probability of 25%.

```csharp
using var openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(builder => builder
                .AddActivitySource("companyname.product.library")
                .SetSampler(new ProbabilityActivitySampler(.25))
                .UseConsoleExporter());
```

  The above requires import of the namespace `OpenTelemetry.Trace.Samplers`.

### Customize Resource

### Filtering and enriching activities using Processor

### OpenTelemetry Instrumentation

This should link to the Instrumentation documentation.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
