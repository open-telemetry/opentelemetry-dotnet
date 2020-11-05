# OWIN Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Owin.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Owin.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Owin)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments [OWIN/Katana](https://github.com/aspnet/AspNetKatana/) and
collect telemetry about incoming web requests.

## Steps to enable OpenTelemetry.Instrumentation.Owin

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Owin`](https://www.nuget.org/packages/opentelemetry.instrumentation.owin)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Owin
```

## References

* [Open Web Interface for .NET](http://owin.org/)
* [Katana Project](https://github.com/aspnet/AspNetKatana/)
* [OpenTelemetry Project](https://opentelemetry.io/)
