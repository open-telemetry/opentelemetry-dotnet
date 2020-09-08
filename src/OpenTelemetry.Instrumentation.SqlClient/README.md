# SqlClient Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments
[Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient)
and
[System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient)
and collects telemetry about database operations.

## Steps to enable OpenTelemetry.Instrumentation.SqlClient

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.SqlClient`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.SqlClient
```

### Step 2: Enable SqlClient Instrumentation at application startup

SqlClient instrumentation must be enabled at application startup.

The following example demonstrates adding SqlClient instrumentation to a
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
            .AddSqlClientInstrumentation()
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
`SqlClientInstrumentationOptions`.

TODO - describe options

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
