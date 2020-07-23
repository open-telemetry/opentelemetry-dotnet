# StackExchange.Redis Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.StackExchangeRedis.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.StackExchangeRedis.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)

Automatically instruments the outgoing calls to Redis made using
`StackExchange.Redis` library.

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.StackExchangeRedis
```

## Configuration

```csharp
// Connect to the server.
using var connection = ConnectionMultiplexer.Connect("localhost:6379");

// Pass the connection to AddRedisInstrumentation.
using var openTelemetry = OpenTelemetrySdk.CreateTracerProvider(b => b
    .AddRedisInstrumentation(connection)
    .UseZipkinExporter()
    .SetResource(Resources.CreateServiceResource("my-service"));
```

For a more detailed example see
[TestRedis](../../examples/Console/TestRedis.cs).

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
