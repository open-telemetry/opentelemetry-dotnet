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

### SetStoredProcedureCommandName

By default, when CommandType is CommandType.StoredProcedure this
instrumentation will set the `db.statement` attribute to the stored procedure
command name. This behavior can be disabled by setting the
`SetStoredProcedureCommandName` to false.

The following example shows how to use `SetStoredProcedureCommandName`.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetStoredProcedureCommandName = false)
    .AddConsoleExporter()
    .Build();
```

### SetTextCommandContent

By default, when CommandType is CommandType.Text, this instrumentation will not
set the `db.statement` attribute. This behavior can be enabled by setting
`SetTextCommandContent` to true.

For .NET Framework, `SetTextCommandContent` is unavailable when using
System.Data.SqlClient. It is only available when using
[`Microsoft.Data.SqlClient`](https://www.nuget.org/packages/Microsoft.Data.SqlClient/).
`SetTextCommandContent` is fully functional in .NET Core when using either
System.Data.SqlClient or Microsoft.Data.SqlClient.

The following example shows how to use `SetTextCommandContent`.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetTextCommandContent = true)
    .AddConsoleExporter()
    .Build();
```

### EnableConnectionLevelAttributes

By default, `EnabledConnectionLevelAttributes` is disabled and this
instrumentation sets the `peer.service` attribute to the
[`DataSource`](https://docs.microsoft.com/dotnet/api/system.data.common.dbconnection.datasource)
property of the connection. If `EnabledConnectionLevelAttributes` is enabled,
the `DataSource` will be parsed and the server name will be sent as the
`net.peer.name` or `net.peer.ip` attribute, the instance name will be sent as
the `db.mssql.instance_name` attribute, and the port will be sent as the
`net.peer.port` attribute if it is not 1433 (the default port).

The following example shows how to use `EnableConnectionLevelAttributes`.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.EnableConnectionLevelAttributes = true)
    .AddConsoleExporter()
    .Build();
```

### Enrich

This option, available on .NET Core only, allows one to enrich the activity
with additional information from the raw `SqlCommand` object. The `Enrich`
action is called only when `activity.IsAllDataRequested` is `true`. It contains
the activity itself (which can be enriched), the name of the event, and the
actual raw object.

Currently there is only one event name reported, "OnCustom". The actual object
is `Microsoft.Data.SqlClient.SqlCommand` for `Microsoft.Data.SqlClient` and
`System.Data.SqlClient.SqlCommand` for `System.Data.SqlClient`.

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(opt => opt.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnCustom"))
        {
            if (rawObject is SqlCommand cmd)
            {
                activity.SetTag("db.commandTimeout", cmd.CommandTimeout);
            }
        };
    })
    .Build();
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `SqlCommand` object.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
