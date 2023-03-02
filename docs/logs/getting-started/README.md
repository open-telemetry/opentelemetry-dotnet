# Getting Started with OpenTelemetry .NET Logs in 5 Minutes

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started
cd getting-started
dotnet run
```

You should see the following output:

```text
Hello World!
```

Install the latest `Microsoft.Extensions.Logging` package:

  ```sh
  dotnet add package Microsoft.Extensions.Logging
  ```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the log output
on the console.

```text
LogRecord.Timestamp:               2023-01-21T00:33:08.1467491Z
LogRecord.CategoryName:            GettingStarted.Program
LogRecord.LogLevel:                Information
LogRecord.State (Key:Value):
    name: tomato
    price: 2.99
    OriginalFormat (a.k.a Body): Hello from {name} {price}.
LogRecord.EventId:                 123
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program creates a
[`LoggerFactory`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.iloggerfactory)
with OpenTelemetry added as a
[LoggerProvider](https://docs.microsoft.com/dotnet/core/extensions/logging-providers).
This `LoggerFactory` is used to create an
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
instance, which is then used to do the logging. The log is sent to the
`OpenTelemetryLoggerProvider`, which is configured to export logs to
`ConsoleExporter`. `ConsoleExporter` simply displays it on the console.

> **Note**
> Certain types of applications (e.g. [ASP.NET
Core](https://learn.microsoft.com/aspnet/core) and [.NET
Worker](https://learn.microsoft.com/dotnet/core/extensions/workers)) have an
`ILogger` based logging pipeline set up by default. In such apps, enabling
OpenTelemetry should be done by adding OpenTelemetry as a provider to the
*existing* logging pipeline, and users should not create a new `LoggerFactory`
(which sets up a totally new logging pipeline). Also, obtaining `ILogger`
instance could be done differently as well. See [Example ASP.NET Core
application](../../../examples/AspNetCore/Program.cs) for an example which shows
how to add OpenTelemetry to the logging pipeline already setup by the
application.

## Learn more

* [Compile-time logging source generation](../source-generation/README.md)
* [Customizing the OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
