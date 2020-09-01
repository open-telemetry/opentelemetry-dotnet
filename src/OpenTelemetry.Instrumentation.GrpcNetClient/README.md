# Grpc.Net.Client Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library)
which instruments [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client)
and collects telemetry about outgoing gRPC requests.

## Steps to enable OpenTelemetry.Instrumentation.GrpcNetClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.GrpcNetClient`](https://www.nuget.org/packages/opentelemetry.instrumentation.grpcnetclient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.GrpcNetClient
```

### Step 2: Enable Grpc.Net.Client Instrumentation at application startup

Grpc.Net.Client instrumentation must be enabled in your code. The following
example demonstrates how to do this for an ASP.NET Core application. The
example also sets up the OpenTelemetry Console exporter and ASP.NET Core
instrumentation which require adding additional packages the following
additional packages:

* [`OpenTelemetry.Instrumentation.AspNetCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
* [`OpenTelemetry.Extensions.Hosting`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
* [`OpenTelemetry.Exporter.Console`](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)

```csharp
using OpenTelemetry.Trace;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOpenTelemetryTracerProvider(builder => {
            builder
                .AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddConsoleExporter();
        });
    }
}
```

## References

* [gRPC for .NET](https://github.com/grpc/grpc-dotnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
