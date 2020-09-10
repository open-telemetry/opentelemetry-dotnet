# Grpc.Net.Client Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library)
which instruments [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client)
and collects telemetry about outgoing gRPC requests.

## Supported .NET Versions

This package targets
[`NETSTANDARD2.1`](https://docs.microsoft.com/dotnet/standard/net-standard#net-implementation-support)
and hence can be used in any .NET versions implementing `NETSTANDARD2.1`.

## Steps to enable OpenTelemetry.Instrumentation.GrpcNetClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.GrpcNetClient`](https://www.nuget.org/packages/opentelemetry.instrumentation.grpcnetclient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.GrpcNetClient
```

### Step 2: Enable Grpc.Net.Client Instrumentation at application startup

Grpc.Net.Client instrumentation must be enabled at application startup.

The following example demonstrates adding Grpc.Net.Client instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using Sdk.CreateTracerProviderBuilder()
            .AddGrpcClientInstrumentation()
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

## References

* [gRPC for .NET](https://github.com/grpc/grpc-dotnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
