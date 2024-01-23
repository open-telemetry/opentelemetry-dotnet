# OpenTelemetry .NET Logs

## Best Practices

The following tutorials have demonstrated the best practices for logging with
OpenTelemetry .NET:

* [Getting Started - Console Application](./getting-started-console/README.md)
* [Getting Started - ASP.NET Core
  Application](./getting-started-aspnetcore/README.md)
* [Logging with Complex Objects](./complex-objects/README.md)

## Structured Logging

:heavy_check_mark: You should use structured logging.

* Structured logging is more efficient than unstructured logging.
  * Filtering and redaction can happen on invidual key-value pairs instead of
    the entire log message.
  * Storage and indexing are more efficient.
* Structured logging makes it easier to manage and consume logs.

:stop_sign: Avoid string interpolation.

> [!WARNING] The following code has bad performance due to [string
> interpolation](https://learn.microsoft.com/dotnet/csharp/tutorials/string-interpolation):

```csharp
var food = "tomato";
var price = 2.99;

logger.LogInformation($"Hello from {food} {price}.");
```

Refer to the [logging performance
benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

## Package Version

:heavy_check_mark: You should always use the
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
interface from the latest stable version of
[Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)
package, regardless of the .NET runtime version being used:

* If you're using the latest stable version of [OpenTelemetry .NET
  SDK](../../src/OpenTelemetry/README.md), you don't have to worry about the
  version of `Microsoft.Extensions.Logging` package because it is already taken
  care of for you via [package dependency](../../Directory.Packages.props).
* Starting from version `3.1.0`, the .NET runtime team is holding a high bar for
  backward compatibility on `Microsoft.Extensions.Logging` even during major
  version bumps, so compatibility is not a concern here.

## API Usage

:heavy_check_mark: You should use [compile-time logging source
generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
pattern to achieve the best performance.

```csharp
public static partial class Food
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from {food} {price}.")]
    public static partial void SayHello(ILogger logger, string food, double price);
}

var food = "tomato";
var price = 2.99;

Food.SayHello(logger, food, price);
```

> [!NOTE]
> There is no need to pass in an explicit
[EventId](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.eventid)
while using
[LoggerMessageAttribute](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggermessageattribute).
A durable `EventId` will be automatically assigned based on the hash of the
method name during code generation.

:stop_sign: Avoid the extension methods from
[LoggerExtensions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggerextensions),
these methods are not optimized for performance.

> [!WARNING]
> The following code has bad performance due to
> [boxing](https://learn.microsoft.com/dotnet/csharp/programming-guide/types/boxing-and-unboxing):

```csharp
var food = "tomato";
var price = 2.99;

logger.LogInformation("Hello from {food} {price}.", food, price);
```

Refer to the [logging performance
benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

:heavy_check_mark: Use
[`LogPropertiesAttribute`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.logpropertiesattribute)
from
[Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions/)
if you need to log complex objects. Check out the [Logging with Complex
Objects](./complex-objects/README.md) tutorial for more details.

### Instruments should be singleton

Instruments SHOULD only be created once and reused throughout the application
lifetime. This [example](../../docs/metrics/getting-started-console/Program.cs)
shows how an instrument is created as a `static` field and then used in the
application. You could also look at this ASP.NET Core
[example](../../examples/AspNetCore/Program.cs) which shows a more Dependency
Injection friendly way of doing this by extracting the `Meter` and an instrument
into a dedicated class called
[Instrumentation](../../examples/AspNetCore/Instrumentation.cs) which is then
added as a `Singleton` service.

### Ordering of tags

When emitting metrics with tags, DO NOT change the order in which you provide
tags. Changing the order of tag keys would increase the time taken by the SDK to
record the measurement.

```csharp
// If you emit the tag keys in this order: name -> color -> taste, stick to this order of tag keys for subsequent measurements.
MyFruitCounter.Add(5, new("name", "apple"), new("color", "red"), new("taste", "sweet"));
...
...
...
// Same measurement with the order of tags changed: color -> name -> taste. This order of tags is different from the one that was first encountered by the SDK.
MyFruitCounter.Add(7, new("color", "red"), new("name", "apple"), new("taste", "sweet")); // <--- DON'T DO THIS
```
