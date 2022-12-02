# OpenTelemetry .NET DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.DependencyInjection.svg)](https://www.nuget.org/packages/OpenTelemetry.DependencyInjection)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.DependencyInjection.svg)](https://www.nuget.org/packages/OpenTelemetry.DependencyInjection)

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.DependencyInjection
```

## Overview

The OpenTelemetry.DependencyInjection package provides extension methods and
helpers for building `TracerProvider`s and `MeterProvider`s using the
Microsoft.Extensions.DependencyInjection API.

The Microsoft.Extensions.DependencyInjection package is primarily intended for
library authors who need to integrate with the OpenTelemetry SDK. For more
details see: [Registration extension method guidance for library
authors](../../docs/trace/extending-the-sdk/README.md#registration-extension-method-guidance-for-library-authors).

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
