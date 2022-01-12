# Traces

## Exporter

OpenTelemetry .NET SDK has provided the following built-in trace exporters

- [Jaeger](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Jaeger/README.md)
- [Zipkin](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Zipkin/README.md)
- [OpenTelemetryProtocol](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)

[Exporter Requirements](./Introduction.md#ExporterRequirements)

- Exporters should use `Activity.TagObjects` collection instead of
  `Activity.Tags` to obtain the full set of attributes (tags).
- Exporters should use `ParentProvider.GetResource()` to get the `Resource`
  associated with the provider.

Here is a demo exporter which simply writes activity names to the console

```{literalinclude} ../../trace/extending-the-sdk/MyExporter.cs
:language: c#
:lines: 17-
```

Apart from the exporter itself, you should also provide extension methods like this

```{literalinclude} ../../trace/extending-the-sdk/MyExporterExtensions.cs
:language: c#
:lines: 17-
```

This allows users to add the Exporter to the `TracerProvider` shown here

```{literalinclude} ../../trace/extending-the-sdk/Program.cs
:language: c#
:lines: 17-
```

## Processor

- Inherit from `OpenTelemetry.BaseProcessor<Activity>`

Here is a demo processor

```{literalinclude} ../../trace/extending-the-sdk/MyProcessor.cs
:language: c#
:lines: 17-
```

### Filtering Processor

A common use case of writing custom processor is to filter Activities from
being exported. Such a "FilteringProcessor" can be written as a wrapper around
an underlying processor. Here is an example

```{literalinclude} ../../trace/extending-the-sdk/MyFilteringProcessor.cs
:language: c#
:lines: 17-
```

When using such a filtering processor, instead of using extension method to
register the exporter, they must be registered manually like this

<!-- TODO include from source code -->

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new MySampler())
    .AddSource("OTel.Demo")
    .AddProcessor(new MyFilteringProcessor(
        new SimpleActivityExportProcessor(new MyExporter("ExporterX")),
        (act) => true))
    .Build();
```

## Sampler

Here is a demo sampler

```{literalinclude} ../../trace/extending-the-sdk/MySampler.cs
:language: c#
:lines: 17-
```
