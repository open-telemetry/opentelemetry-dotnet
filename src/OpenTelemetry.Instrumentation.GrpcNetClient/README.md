# Grpc.Net.Client Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
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
exporter and adds instrumentation for HttpClient, which requires adding the
packages
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
and
[`OpenTelemetry.Instrumentation.Http`](../OpenTelemetry.Instrumentation.Http/README.md)
to the application. As Grpc.Net.Client uses HttpClient underneath, it is
recommended to enable HttpClient instrumentation as well to ensure proper
context propagation. This would cause an activity being produced for both a gRPC
call and its underlying HTTP call. This behavior can be
[configured](#suppressdownstreaminstrumentation).

```csharp
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddGrpcClientInstrumentation()
            .AddHttpClientInstrumentaiton()
            .AddConsoleExporter()
            .Build();
    }
}
```

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](../OpenTelemetry.Instrumentation.AspNetCore/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`GrpcClientInstrumentationOptions`.

### SuppressDownstreamInstrumentation

This option prevents downstream instrumentation from being invoked.
Grpc.Net.Client is built on top of HttpClient. When instrumentation for both
libraries is enabled, `SuppressDownstreamInstrumentation` prevents the
HttpClient instrumentation from generating an additional activity. Additionally,
since HttpClient instrumentation is normally responsible for propagating context
(ActivityContext and Baggage), Grpc.Net.Client instrumentation propagates
context when `SuppressDownstreamInstrumentation` is enabled.

The following example shows how to use `SuppressDownstreamInstrumentation`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddGrpcClientInstrumentation(
        opt => opt.SuppressDownstreamInstrumentation = true)
    .AddHttpClientInstrumentation()
    .Build();
```

### Enrich

This option allows one to enrich the activity with additional information
from the raw `HttpRequestMessage` object. The `Enrich` action is called only
when `activity.IsAllDataRequested` is `true`. It contains the activity itself
(which can be enriched), the name of the event, and the actual raw object.
For event name "OnStartActivity", the actual object will be
`HttpRequestMessage`.

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
services.AddOpenTelemetryTracing((builder) =>
{
    builder
    .AddGrpcClientInstrumentation(opt => opt.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnStartActivity"))
        {
            if (rawObject is HttpRequestMessage request)
            {
                activity.SetTag("requestVersion", request.Version);
            }
        }
    })
});
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `HttpRequest` and `HttpResponse`.

## References

* [gRPC for .NET](https://github.com/grpc/grpc-dotnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
