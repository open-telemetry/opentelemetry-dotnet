# StackExchange.Redis Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.StackExchangeRedis.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.StackExchangeRedis.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments
[StackExchange.Redis](https://www.nuget.org/packages/StackExchange.Redis/)
and collects telemetry about outgoing calls to Redis.

## Steps to enable OpenTelemetry.Instrumentation.StackExchangeRedis

## Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.StackExchangeRedis`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.StackExchangeRedis
```

## Step 2: Enable StackExchange.Redis Instrumentation at application startup

StackExchange.Redis instrumentation must be enabled at application startup.

The following example demonstrates adding StackExchange.Redis instrumentation
to a console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        // Connect to the server.
        using var connection = ConnectionMultiplexer.Connect("localhost:6379");

        using Sdk.CreateTracerProviderBuilder()
            .AddRedisInstrumentation(connection)
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](../OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to documentation for [OpenTelemetry.Instrumentation.AspNet](../OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`StackExchangeRedisCallsInstrumentationOptions`.

TODO - describe options

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
