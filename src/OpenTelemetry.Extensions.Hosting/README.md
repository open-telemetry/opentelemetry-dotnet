# OpenTelemetry.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Hosting.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Extensions.Hosting
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

### Current OpenTelemetry SDK v1.4.0 and newer extensions

Targeting `OpenTelemetry.OpenTelemetryBuilder`:

* `StartWithHost`: Registers an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start tracing and/or metric services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

### Obsolete OpenTelemetry SDK pre-1.4.0 extensions

> **Note**
> The below extension methods should be called by application host code
only. Library authors see: [Registration extension method guidance for library
authors](../../docs/trace/extending-the-sdk/README.md#registration-extension-method-guidance-for-library-authors).
<!-- This comment is to make sure the two notes above and below are not merged -->
> **Note**
> Multiple calls to the below extensions will **NOT** result in multiple
providers. To establish multiple providers use the
`Sdk.CreateTracerProviderBuilder()` and/or `Sdk.CreateMeterProviderBuilder()`
methods. See [TracerProvider
configuration](../../docs/trace/customizing-the-sdk/README.md#tracerprovider-configuration)
and [Building a
MeterProvider](../../docs/metrics/customizing-the-sdk/README.md#building-a-meterprovider)
for more details.

Targeting `Microsoft.Extensions.DependencyInjection.IServiceCollection`:

* `AddOpenTelemetryTracing`: Configure OpenTelemetry and register an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start tracing services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

* `AddOpenTelemetryMetrics`: Configure OpenTelemetry and register an
  [IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
  to automatically start metric services in the supplied
  [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

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
    .WithTracing(builder => builder.AddConsoleExporter())
    .WithMetrics(builder => builder.AddConsoleExporter())
    .StartWithHost();

var app = appBuilder.Build();

app.Run();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
