# Getting Started with OpenTelemetry .NET Logs in 5 Minutes - Console Application

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

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs).

Run the application again (using `dotnet run`) and you should see the log output
on the console.

```text
LogRecord.Timestamp:               2023-09-15T06:07:03.5502083Z
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

LogRecord.Timestamp:               2023-09-15T06:07:03.5683511Z
LogRecord.CategoryName:            Program
LogRecord.Severity:                Fatal
LogRecord.SeverityText:            Critical
LogRecord.Body:                    A `{productType}` recall notice was published for `{brandName} {productDescription}` produced by `{companyName}` ({recallReasonDescription}).
LogRecord.Attributes (Key:Value):
    brandName: Contoso
    productDescription: Salads
    productType: Food & Beverages
    recallReasonDescription: due to a possible health risk from Listeria monocytogenes
    companyName: Contoso Fresh Vegetables, Inc.
    OriginalFormat (a.k.a Body): A `{productType}` recall notice was published for `{brandName} {productDescription}` produced by `{companyName}` ({recallReasonDescription}).
LogRecord.EventId:                 1338249384
LogRecord.EventName:               FoodRecallNotice

...
```

Congratulations! You are now collecting logs using OpenTelemetry.

What does the above program do?

The program has created a logging pipeline by instantiating a
[`LoggerFactory`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.iloggerfactory)
instance, with OpenTelemetry added as a [logging
provider](https://docs.microsoft.com/dotnet/core/extensions/logging-providers).
OpenTelemetry SDK is then configured with a
[ConsoleExporter](../../../src/OpenTelemetry.Exporter.Console/README.md) to
export the logs to the console for demonstration purpose (note: ConsoleExporter
is not intended for production usage, other exporters such as [OTLP
Exporter](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
should be used instead).

The `LoggerFactory` instance is used to create an
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
instance, which is used to do the actual logging.

Following the .NET logging best practice, [compile-time logging source
generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
has been used across the example, which delivers high performance, structured
logging, and type-checked parameters:

```csharp
internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Food `{name}` price changed to `{price}`.")]
    public static partial void FoodPriceChanged(this ILogger logger, string name, double price);

    ...
}
```

> [!NOTE]
> For applications which use `ILogger` with [dependency injection
(DI)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
(e.g. [ASP.NET Core](https://learn.microsoft.com/aspnet/core) and [.NET
Worker](https://learn.microsoft.com/dotnet/core/extensions/workers)), the common
practice is to add OpenTelemetry as a [logging
provider](https://docs.microsoft.com/dotnet/core/extensions/logging-providers)
to the DI logging pipeline, rather than set up a completely new logging pipeline
by creating a new `LoggerFactory` instance.
>
> Refer to the [Getting Started with OpenTelemetry .NET Logs in 5 Minutes -
ASP.NET Core Application](../getting-started-aspnetcore/README.md) tutorial to
learn more.

## Learn more

* [Logging in C# and .NET](https://learn.microsoft.com/dotnet/core/extensions/logging)
* [Logging with Complex Objects](../complex-objects/README.md)
* [Customizing the OpenTelemetry .NET SDK](../customizing-the-sdk/README.md)
* [Extending the OpenTelemetry .NET SDK](../extending-the-sdk/README.md)
