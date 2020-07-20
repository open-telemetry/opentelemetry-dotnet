# Using StackExchange.Redis instrumentation

Outgoing calls to Redis made using `StackExchange.Redis` library can be
automatically tracked.

1. Install package to your project:
   [OpenTelemetry.Instrumentation.StackExchangeRedis](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)

2. Configure Redis instrumentation:

    ```csharp
    // Connect to the server.
    using var connection = ConnectionMultiplexer.Connect("localhost:6379");

    // Pass the connection to AddRedisInstrumentation.
    using var openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(b => b
        .AddRedisInstrumentation(connection)
        .UseZipkinExporter()
        .SetResource(Resources.CreateServiceResource("my-service"));
    ```

For a more detailed example see
[TestRedis](../../samples/Exporters/Console/TestRedis.cs).
