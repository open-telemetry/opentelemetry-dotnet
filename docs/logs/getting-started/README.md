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
API to log a formatted string with a severity level of Information. Click
[here](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
for more information on the different logs levels. Opentelemetry captures this
and sends it to `ConsoleExporter`. `ConsoleExporter` simply displays it on the
console.

## Learn more

* If you want to build a custom exporter/processor/sampler, refer to [extending
  the SDK](../extending-the-sdk/README.md).
* If you want to customize the SDK, refer to [customizing
  the SDK](../customizing-the-sdk/README.md).
