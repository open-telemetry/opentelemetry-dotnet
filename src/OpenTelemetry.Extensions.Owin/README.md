# Telemetry correlation library for OWIN/Katana

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Owin.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Owin)
[![NuGet](https://img.shields.io/nuget/dt/OOpenTelemetry.Extensions.Owin.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Owin)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments the [OWIN/Katana](https://github.com/aspnet/AspNetKatana/)
and notifies listeners about incoming web requests.

## Steps to enable OpenTelemetry.Extensions.Owin

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Extensions.Owin`](https://www.nuget.org/packages/opentelemetry.extensions.owin)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Extensions.Owin
```

## References

* [Open Web Interface for .NET](http://owin.org/)
* [Katana Project](https://github.com/aspnet/AspNetKatana/)
* [OpenTelemetry Project](https://opentelemetry.io/)
