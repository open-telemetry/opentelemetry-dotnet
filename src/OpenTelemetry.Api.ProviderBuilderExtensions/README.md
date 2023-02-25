# OpenTelemetry.Api.ProviderBuilderExtensions

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Api.ProviderBuilderExtensions.svg)](https://www.nuget.org/packages/OpenTelemetry.Api.ProviderBuilderExtensions)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Api.ProviderBuilderExtensions.svg)](https://www.nuget.org/packages/OpenTelemetry.Api.ProviderBuilderExtensions)

## Installation

```shell
dotnet add package OpenTelemetry.Api.ProviderBuilderExtensions
```

## Overview

The `OpenTelemetry.Api.ProviderBuilderExtensions` package provides extension
methods and helpers for building `TracerProvider`s and `MeterProvider`s using
the `Microsoft.Extensions.DependencyInjection` API (primarily
[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)).

The `OpenTelemetry.Api.ProviderBuilderExtensions` package is intended for
instrumentation library authors who need to integrate with the OpenTelemetry SDK
without a direct dependency. For more details see: [Registration extension
method guidance for library
authors](../../docs/trace/extending-the-sdk/README.md#registration-extension-method-guidance-for-library-authors).

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
