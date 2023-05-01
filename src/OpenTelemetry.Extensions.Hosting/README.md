# OpenTelemetry.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Hosting
```

## Overview

The OpenTelemetry.Extensions.Hosting package provides extension methods for
automatically starting (and stopping) OpenTelemetry tracing (`TracerProvider`)
and metrics (`MeterProvider`) in [ASP.NET
 Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host) and
 [.NET Generic](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
 hosts. These are completely optional extensions meant to simplify the
 management of the OpenTelemetry SDK lifecycle.

## Extension method reference

Targeting `Microsoft.Extensions.DependencyInjection.IServiceCollection`:

* `AddOpenTelemetry`: Registers an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start tracing and/or metric services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
  and then returns an `OpenTelemetryBuilder` class.

  > **Note**
  > `AddOpenTelemetry` should be called by application host code only. Library
  authors see: [Registration extension method guidance for library
  authors](../../docs/trace/extending-the-sdk/README.md#registration-extension-method-guidance-for-library-authors).
  <!-- This comment is to make sure the two notes above and below are not merged
  -->
  > **Note**
  > Multiple calls to `AddOpenTelemetry` will **NOT** result in multiple
  providers. Only a single `TracerProvider` and/or `MeterProvider` will be
  created in the target `IServiceCollection`. To establish multiple providers
  use the `Sdk.CreateTracerProviderBuilder()` and/or
  `Sdk.CreateMeterProviderBuilder()` methods. See [TracerProvider
  configuration](../../docs/trace/customizing-the-sdk/README.md#tracerprovider-configuration)
  and [Building a
  MeterProvider](../../docs/metrics/customizing-the-sdk/README.md#building-a-meterprovider)
  for more details.

  `OpenTelemetryBuilder` methods:

  * `ConfigureResource`: Registers a callback action to configure the
  `ResourceBuilder` for tracing and metric providers.

  * `WithTracing`: Enables tracing and optionally configures the
  `TracerProvider`.

  * `WithMetrics`: Enables metrics and optionally configures the
  `MeterProvider`.

## Usage

The following example shows how to register OpenTelemetry tracing & metrics in
an ASP.NET Core host using the OpenTelemetry.Extensions.Hosting extensions.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(builder => builder.AddService(serviceName: "MyService"))
    .WithTracing(builder => builder.AddConsoleExporter())
    .WithMetrics(builder => builder.AddConsoleExporter());

var app = appBuilder.Build();

app.Run();
```

A fully functional example can be found
[here](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/AspNetCore).

### Resources

To dynamically add resources at startup from the dependency injection you can
provide an `IResourceDetector`.
To make use of it add it to the dependency injection and they you can use the
`ISerivceProvider` add it to OpenTelemetry:

```csharp
public class MyResourceDetector : IResourceDetector
{
    private readonly IWebHostEnvironment webHostEnvironment;

    public MyResourceDetector(IWebHostEnvironment webHostEnvironment)
    {
        this.webHostEnvironment = webHostEnvironment;
    }

    public Resource Detect()
    {
        return ResourceBuilder.CreateEmpty()
            .AddService(serviceName: this.webHostEnvironment.ApplicationName)
            .AddAttributes(new Dictionary<string, object> { ["host.environment"] = this.webHostEnvironment.EnvironmentName })
            .Build();
    }
}

services.AddSingleton<MyResourceDetector>();

services.AddOpenTelemetry()
    .ConfigureResource(builder =>
        builder.AddDetector(sp => sp.GetRequiredService<MyResourceDetector>()))
    .WithTracing(builder => builder.AddConsoleExporter())
    .WithMetrics(builder => builder.AddConsoleExporter());
```

## Migrating from pre-release versions of OpenTelemetry.Extensions.Hosting

Pre-release versions (all versions prior to 1.4.0) of
`OpenTelemetry.Extensions.Hosting` contained signal-specific methods for
configuring tracing and metrics:

* `AddOpenTelemetryTracing`: Configure OpenTelemetry and register an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start tracing services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

* `AddOpenTelemetryMetrics`: Configure OpenTelemetry and register an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start metric services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

These methods were marked obsolete and later removed. You should migrate your
code to the new `AddOpenTelemetry` method documented above. Refer the
[old](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.3.2/examples/AspNetCore/Program.cs)
and
[new](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/AspNetCore)
versions of the example application to assist you in your migration.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
