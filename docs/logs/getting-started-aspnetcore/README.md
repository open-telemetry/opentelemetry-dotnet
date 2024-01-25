# Getting Started with OpenTelemetry .NET Logs in 5 Minutes - ASP.NET Core Application

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new web application:

```sh
dotnet new web -o aspnetcoreapp
cd aspnetcoreapp
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
and
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
packages:

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application (using `dotnet run`) and then browse to the URL shown in the
console for your application (e.g. `http://localhost:5000`). You should see the
logs output from the console:

```text
LogRecord.Timestamp:               2023-09-06T22:59:17.9787564Z
LogRecord.CategoryName:            getting-started-aspnetcore
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Starting the app...
LogRecord.Attributes (Key:Value):
    OriginalFormat (a.k.a Body): Starting the app...
LogRecord.EventId:                 225744744
LogRecord.EventName:               StartingApp

...

LogRecord.Timestamp:               2023-09-06T22:59:18.0644378Z
LogRecord.CategoryName:            Microsoft.Hosting.Lifetime
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Now listening on: {address}
LogRecord.Attributes (Key:Value):
    address: http://localhost:5000
    OriginalFormat (a.k.a Body): Now listening on: {address}
LogRecord.EventId:                 14
LogRecord.EventName:               ListeningOnAddress

...

LogRecord.Timestamp:               2023-09-06T23:00:46.1639248Z
LogRecord.TraceId:                 3507087d60ae4b1d2f10e68f4e40784a
LogRecord.SpanId:                  c51be9f19c598b69
LogRecord.TraceFlags:              None
LogRecord.CategoryName:            Program
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Food `{name}` price changed to `{price}`.
LogRecord.Attributes (Key:Value):
    name: artichoke
    price: 9.99
    OriginalFormat (a.k.a Body): Food `{name}` price changed to `{price}`.
LogRecord.EventId:                 344095174
LogRecord.EventName:               FoodPriceChanged

...
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program has cleared the default [logging
providers](https://learn.microsoft.com/dotnet/core/extensions/logging-providers)
then added OpenTelemetry as a logging provider to the ASP.NET Core logging
pipeline. OpenTelemetry SDK is then configured with a
[ConsoleExporter](../../../src/OpenTelemetry.Exporter.Console/README.md) to
export the logs to the console for demonstration purpose (note: ConsoleExporter
is not intended for production usage, other exporters such as [OTLP
Exporter](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
should be used instead). From the console output we can see logs from both our
logger and the ASP.NET Core framework loggers, as indicated by the
`LogRecord.CategoryName`.

The example has demonstrated the best practice from ASP.NET Core by injecting
generic `ILogger<T>`:

```csharp
app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.FoodPriceChanged("artichoke", 9.99);

    return "Hello from OpenTelemetry Logs!";
});
```

Following the .NET logging best practice, [compile-time logging source
generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
has been used across the example, which delivers high performance, structured
logging, and type-checked parameters:

```csharp
public static partial class ApplicationLogs
{
    [LoggerMessage(LogLevel.Information, "Starting the app...")]
    public static partial void StartingApp(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);
}
```

For logs that occur between `builder.Build()` and `app.Run()` when injecting a
generic `ILogger<T>` is not an option, `app.Logger` is used instead:

```csharp
app.Logger.StartingApp();
```

> [!NOTE]
> There are cases where logging is needed before the [dependency injection
(DI)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
logging pipeline is available (e.g. before `builder.Build()`) or after the DI
logging pipeline is disposed (e.g. after `app.Run()`). The common practice is to
use a separate logging pipeline by creating a `LoggerFactory` instance.
>
> Refer to the [Getting Started with OpenTelemetry .NET Logs in 5 Minutes -
Console Application](../getting-started-console/README.md) tutorial to learn
more about how to create a `LoggerFactory` instance and configure OpenTelemetry
to work with it.

## Learn more

* [Logging in C# and .NET](https://learn.microsoft.com/dotnet/core/extensions/logging)
* [Logging with Complex Objects](../complex-objects/README.md)
* [Customizing the OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
