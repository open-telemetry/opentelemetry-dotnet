# Declarative configuration example

This example builds its tracing pipeline from a YAML file, using the
[OpenTelemetry.Configuration.Declarative](../../src/OpenTelemetry.Configuration.Declarative/README.md)
package. The application has no tracing code: it does not call `WithTracing`,
`AddSource` or `AddConsoleExporter`. Its only OpenTelemetry code is:

```csharp
builder.Services.AddOpenTelemetry().UseDeclarativeConfiguration("otel-config.yaml");
```

`otel-config.yaml` turns tracing on, listens to the example's own
`ActivitySource`, and exports spans to the console.

## Run the example

```sh
dotnet run
```

The `SayHello` span is printed to the console, with the `service.name`
resource attribute from the file.

## Use OTEL_CONFIG_FILE instead of a path

Call `UseDeclarativeConfiguration()` without a path, and set
`OTEL_CONFIG_FILE` in the process environment:

```csharp
builder.Services.AddOpenTelemetry().UseDeclarativeConfiguration();
```

```sh
OTEL_CONFIG_FILE=otel-config.yaml dotnet run
```

A relative path is resolved against the build output directory, which is where
the project copies `otel-config.yaml`.

## Listen to every source

Remove the `tracer_configurator/development` block from the file. Every
`ActivitySource` is then listened to, which is the specification's default. On
.NET 10 and later that includes the runtime's own sources, such as HTTP client
spans.
