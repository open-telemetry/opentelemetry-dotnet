# OpenTelemetry.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Extensions.Hosting
```

## Usage

### Tracing

#### Simple Configuration

The following example registers tracing using the `ZipkinExporter` and binds
options to the "Zipkin" configuration section:

```csharp
services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddZipkinExporter());

services.Configure<ZipkinExporterOptions>(this.Configuration.GetSection("Zipkin"));
```

#### Using Dependency Injection

The following example registers a processor of the type "MyProcessor" which has
been registered as a singleton with the `IServiceCollection`:

```csharp
services.AddSingleton<MyProcessor>();

services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddProcessor<MyProcessor>());
```

Similar methods exist for registering instrumentation (`AddInstrumentation<T>`)
and setting a sampler (`SetSampler<T>`).

You can also access the application `IServiceProvider` directly and accomplish
the same registration using the `ConfigureBuilder` extension like this:

```csharp
services.AddSingleton<MyProcessor>();

services.AddOpenTelemetryTracing(hostingBuilder => hostingBuilder
    .ConfigureBuilder((sp, builder) => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessor(sp.GetRequiredService<MyProcessor>())));
```

**Note:** `ConfigureBuilder` is called _after_ the `IServiceProvider` has been built
from the application `IServiceCollection` so any services registered in the
`ConfigureBuilder` callback will be ignored.

#### Building Extension Methods

Library authors may want to configure the OpenTelemetry `TracerProvider` and
register application services to provide more complex features. This can be
accomplished concisely by using the `TracerProviderBuilder.ConfigureServices`
extension method inside of a more general `TracerProviderBuilder` configuration
extension like this:

```csharp
public static class MyLibraryExtensions
{
    public static TracerProviderBuilder AddMyFeature(this TracerProviderBuilder tracerProviderBuilder)
    {
        return tracerProviderBuilder
            .ConfigureServices(services =>
                services
                    .AddHostedService<MyHostedService>()
                    .AddSingleton<MyService>()
                    .AddSingleton<MyProcessor>()
                    .AddSingleton<MySampler>())
            .AddProcessor<MyProcessor>()
            .SetSampler<MySampler>();
    }
}
```

Such an extension method can be consumed like this:

```csharp
services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddMyFeature()
    .AddZipkinExporter());
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
