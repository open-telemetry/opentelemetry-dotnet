# Grpc.Net.Client Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.GrpcNetClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.GrpcNetClient)

This is an [Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
which instruments [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client)
and collects traces about outgoing gRPC requests.

**Note: This component is based on the OpenTelemetry semantic conventions for
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a [pre-release](../../VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be breaking changes. You can track the progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).**

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
dotnet add package --prerelease OpenTelemetry.Instrumentation.GrpcNetClient
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
            .AddHttpClientInstrumentation()
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

This instrumentation library provides `EnrichWithHttpRequestMessage` and
`EnrichWithHttpResponseMessage` options that can be used to enrich the activity
with additional information from the raw `HttpRequestMessage` and
`HttpResponseMessage` objects respectively. These actions are called only when
`activity.IsAllDataRequested` is `true`. It contains the activity itself (which
can be enriched), the name of the event, and the actual raw object. The
following code snippet shows how to add additional tags using these options.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddGrpcClientInstrumentation(options =>
        {
            options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
            {
                activity.SetTag("requestVersion", httpRequestMessage.Version);
            };
            options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
            {
                activity.SetTag("responseVersion", httpResponseMessage.Version);
            };
        });
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `HttpRequest` and `HttpResponse`.

## References

* [gRPC for .NET](https://github.com/grpc/grpc-dotnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
