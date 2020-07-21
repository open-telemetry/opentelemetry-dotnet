# Using OpenTelemetry SDK to send telemetry

- [Using OpenTelemetry SDK to send
  telemetry](#using-opentelemetry-sdk-to-send-telemetry)
  - [OpenTelemetry SDK](#opentelemetry-sdk)
  - [Basic usage](#basic-usage)
  - [Advanced usage scenarios](#advanced-usage-scenarios)
    - [Customize Exporter](#customize-exporter)
    - [Customize Sampler](#customize-sampler)
    - [Customize Resource](#customize-resource)
    - [Filtering and enriching activities using
      Processor](#filtering-and-enriching-activities-using-processor)
    - [OpenTelemetry Instrumentation](#opentelemetry-instrumentation)

## OpenTelemetry SDK

OpenTelemetry SDK is a reference implementation of the OpenTelemetry API. It
implements the Tracer API, the Metric API, and the Context API. OpenTelemetry
SDK deals with concerns such as sampling, processing pipeline, exporting
telemetry to a particular backend etc. The default implementation consists of
the following.

1. Set of [Built-in
   samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-samplers)
2. Set of [Built-in
   processors](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#built-in-span-processors).
   1. SimpleProcessor which sends Activities to the exporter without any
      batching.
   2. BatchingProcessor which batches and sends Activities to the exporter.
3. Set of [Built-in
   exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-exporter)
   1. Console - Displays activities in console. Used for testing purposes.
   2. Jaeger - Exports traces to [Jaeger](https://www.jaegertracing.io/)
   3. Zipkin- Exports traces to [Zipkin](https://zipkin.io/)
   4. zPages- Exports traces to [zPages](https://opencensus.io/zpages/)
   5. OpenTelemetry Protocol - Exports traces using OpenTelemetry protocol to
      the OpenTelemetry Collector.
   6. Prometheus.- Exports metrics to [Prometheus](https://prometheus.io/)
4. Extensibility options for users to customize SDK.

## Basic usage

The following examples show how to start collecting OpenTelemetry traces from a
console application, and have the traces displayed in the console.

1. Create a console application and install the `OpenTelemetry.Exporter.Console`
   package to your it.

    ```xml
    <ItemGroup>
        <PackageReference
        Include="OpenTelemetry.Exporter.Console" Version="0.3.0" />
    </ItemGroup>
    ```

2. At the beginning of the application, enable OpenTelemetry Sdk with
   ConsoleExporter as shown below. It also configures to collect activities from
   the source named "companyname.product.library".

    ```csharp
    using var openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(
                    (builder) => builder.AddActivitySource("companyname.product.library")
                        .UseConsoleExporter(opt => opt.DisplayAsJson = options.DisplayAsJson));
    ```

3. Generate some activities in the application as shown below.

    ```csharp
        var source = new ActivitySource("companyname.product.library");
        using (var parent = source.StartActivity("ActivityName", ActivityKind.Server))
        {
            parent?.AddTag("http.method", "GET");
        }
    ```

Run the application. Traces will be displayed in the console.

## Advanced usage scenarios

### Customize Exporter

### Customize Sampler

### Customize Resource

### Filtering and enriching activities using Processor

### OpenTelemetry Instrumentation

This should link to the Instrumentation documentation.
