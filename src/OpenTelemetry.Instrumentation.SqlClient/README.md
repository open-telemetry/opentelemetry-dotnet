# SqlClient Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.SqlClient.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
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
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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

### Capturing 'db.statement'

The `SqlClientInstrumentationOptions` class exposes several properties that can be
used to configure how the [`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute is captured upon execution of a query.

#### .NET Core - SetDbStatementForStoredProcedure and SetDbStatementForText

On .NET Core, two properties are available: `SetDbStatementForStoredProcedure`
and `SetDbStatementForText`. These properties control capturing of
`CommandType.StoredProcedure` and `CommandType.Text` respectively.

`SetDbStatementForStoredProcedure` is _true_ by default and will set
[`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute to the stored procedure command name.

`SetDbStatementForText` is _false_ by default (to prevent accidental capture of
sensitive data that might be part of the SQL statement text). When set to
`true`, the instrumentation will set [`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute to the text of the SQL command being executed.

To disable capturing stored procedure commands use configuration like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetDbStatementForStoredProcedure = false)
    .AddConsoleExporter()
    .Build();
```

To enable capturing of `sqlCommand.CommandText` for `CommandType.Text` use the
following configuration.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetDbStatementForText = true)
    .AddConsoleExporter()
    .Build();
```

#### .NET Framework - SetDbStatement

For .NET Framework, `SetDbStatementForStoredProcedure` and
`SetDbStatementForText` are not available. Instead, a single `SetDbStatement`
property should be used to control whether this instrumentation should set the
[`db.statement`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md#call-level-attributes)
attribute to the text of the `SqlCommand` being executed. This could either be
a name of a stored procedure or a full text of a `CommandType.Text` query.

On .NET Framwork, unlike .NET Core, the instrumentation capabilities for both
[`Microsoft.Data.SqlClient`](https://www.nuget.org/packages/Microsoft.Data.SqlClient/)
and `System.Data.SqlClient` are limited:

* [`Microsoft.Data.SqlClient`](https://www.nuget.org/packages/Microsoft.Data.SqlClient/)
  always exposes both the stored procedure name and the full query text but
  doesn't allow for more granular control to turn either on/off depending on
  `CommandType`.
* `System.Data.SqlClient` only exposes stored procedure names and not the full
  query text.

Since `CommandType.Text` might contain sensitive data, all SQL capturing is
_disabled_ by default to protect against accidentally sending full query text
to a telemetry backend. If you are only using stored procedures or have no
sensitive data in your `sqlCommand.CommandText`, you can enable SQL capturing
using the options like below:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.SetDbStatement = true)
    .AddConsoleExporter()
    .Build();
```

## EnableConnectionLevelAttributes

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
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.EnableConnectionLevelAttributes = true)
    .AddConsoleExporter()
    .Build();
```

## Enrich

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

### RecordException

This option, available on .NET Core only, can be set to instruct the instrumentation
to record SqlExceptions as Activity [events](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md).

The default value is `false` and can be changed by the code like below.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSqlClientInstrumentation(
        options => options.RecordException = true)
    .AddConsoleExporter()
    .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)

* [OpenTelemetry semantic conventions for database calls](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/database.md)
