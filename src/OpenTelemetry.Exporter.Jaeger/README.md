# Jaeger Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Jaeger.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger)

The Jaeger exporter communicates to a Jaeger Agent through the compact thrift
protocol on the Compact Thrift API port.

## Supported .NET Versions

This package supports all the officially supported versions of [.NET
Core](https://dotnet.microsoft.com/download/dotnet-core).

For .NET Framework, versions 4.6 and above are supported.

## Prerequisite

* [Get Jaeger](https://www.jaegertracing.io/docs/1.13/getting-started/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Jaeger
```

## Configuration

You can configure the `JaegerExporter` through `JaegerExporterOptions`
properties:

* `AgentHost`: Usually `localhost` since an agent should usually be running on
  the same machine as your application or service.
* `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default
  `6831`).
* `MaxPayloadSizeInBytes`: The maximum size of each UDP packet that gets
  sent to the agent. (default `4096`).
* `ExportProcessorType`: Whether the exporter should use
  [Batch or Simple exporting processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  .
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the
[`TestJaegerExporter.cs`](../../examples/Console/TestJaegerExporter.cs)
for an example of how to use the exporter.

## Resources and Process Tags

The attributes of a [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
 are represented in Jaeger as `ProcessTags` that associate with an
exported span. The exception is the [required](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#semantic-attributes-with-sdk-provided-default-value)
`service.name` attribute which accompanies each exported span as shown below.

Example configuration of resource attributes and attachment to `TracerProvider`
with `JaegerExporter`:

```csharp
 List<KeyValuePair<string, object>> resourceAttrib = new() {
    new KeyValuePair<string, object>("attribute1", "ABC"),
    new KeyValuePair<string, object>("attribute2", 123),
    new KeyValuePair<string, object>("service.name", ".NET OTel Service")
};

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(
        ResourceBuilder.CreateDefault().AddAttributes(resourceAttrib))
    .AddSource("mysource")
    .AddJaegerExporter()
    .Build();
```

When the source configured for above emits two generic, nested activities
the resulting telemetry is captured on Jaeger as shown below. The
`service.name` attribute is not shown with the rest of the attributes as
`ProcessTags`, but rather separately as a header for each span representing the
service it originated from.
![Image of Jaeger UI showing presence of process tags](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Jaeger/jaeger-resource.PNG)

## References

* [Jaeger](https://www.jaegertracing.io)
* [OpenTelemetry Project](https://opentelemetry.io/)
