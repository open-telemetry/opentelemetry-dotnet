# OpenTelemetry.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Hosting
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

The following example adds a processor instance
as a singleton to the `IServiceCollection` which will
be picked up by the TracerProvider.

```csharp
services.AddSingleton<BaseProcessor<Activity>>(new MyProcessor1());
services.AddSingleton<BaseProcessor<Activity>>(new MyProcessor2());

services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation());
```

Similarly, sampler can also be set on the `IServiceCollection`.

```csharp
services.AddSingleton<Sampler>(new TestSampler());
```

You can also access the application `IServiceProvider` directly and accomplish
the same registration using the `Configure` extension like this:

```csharp
services.AddSingleton<MyProcessor>();

services.AddOpenTelemetryTracing(hostingBuilder => hostingBuilder
    .Configure((sp, builder) => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessor(sp.GetRequiredService<MyProcessor>())));
```

**Note:** `Configure` is called _after_ the `IServiceProvider` has been built
from the application `IServiceCollection` so any services registered in the
`Configure` callback will be ignored.

#### Building Extension Methods

Library authors may want to configure the OpenTelemetry `TracerProvider` and
register application services to provide more complex features. This can be
accomplished concisely by using the `TracerProviderBuilder.GetServices`
extension method inside of a more general `TracerProviderBuilder` configuration
extension like this:

```csharp
public static class MyLibraryExtensions
{
    public static TracerProviderBuilder AddMyFeature(this TracerProviderBuilder tracerProviderBuilder)
    {
        (tracerProviderBuilder.GetServices()
            ?? throw new NotSupportedException(
                "MyFeature requires a hosting TracerProviderBuilder instance."))
            .AddHostedService<MyHostedService>()
            .AddSingleton<MyService>()
            .AddSingleton<BaseProcessor<Activity>>(new MyProcessor());
            .AddSingleton<Sampler>(new MySampler());

        return tracerProviderBuilder();
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
