# Getting Started with OpenTelemetry .NET Logs in 5 Minutes

First, download and install the [.NET Core
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
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          2020-11-13T23:50:33.5764463Z
LogRecord.EventId:            0
LogRecord.CategoryName:       Program
LogRecord.LogLevel:           Information
LogRecord.TraceFlags:         None
LogRecord.State:              Hello from tomato 2.99.
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program uses the
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
API to log a formatted string with a severity level of Information. OpenTelemetry
captures this and sends it to the `ConsoleExporter` which displays logs on the console.

## Configure Filtering

OpenTelemetry's provider is `OpenTelemetryLoggerProvider` and filtering rules 
can define the minimum [`LogLevel`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
applied to providers and categories.

### via appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
    },
    "OpenTelemetry": { // alias for OpenTelemetryLoggingProvider
      "LogLevel": {
        "Default": "Error" // Overrides preceding LogLevel:Default
      }
    }
  }
}
```

This example uses the "OpenTelemetry" alias for the `OpenTelemetryLoggingProvider`.
Here OpenTelemetry is given a default of "Error" which overrides the global default "Information".

### via code

```csharp
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("category name", LogLevel.Error);
```

This example defines "Error" as the minimum `LogLevel` for the combination of
`OpenTelemetryLoggerProvider` and a user defined category.

## Learn more

* See also the official guide for [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging)
* If you want to build a custom exporter/processor/sampler, refer to [extending
  the SDK](../extending-the-sdk/README.md).
