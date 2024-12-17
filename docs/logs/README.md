# OpenTelemetry .NET Logs

<details>
<summary>Table of Contents</summary>

* [Best Practices](#best-practices)
* [Package Version](#package-version)
* [Logging API](#logging-api)
  * [ILogger](#ilogger)
  * [LoggerFactory](#loggerfactory)
* [Log Correlation](#log-correlation)
* [Log Enrichment](#log-enrichment)
* [Log Filtering](#log-filtering)
* [Log Redaction](#log-redaction)

</details>

## Best Practices

The following tutorials have demonstrated the best practices for logging with
OpenTelemetry .NET:

* [Getting Started - ASP.NET Core
  Application](./getting-started-aspnetcore/README.md)
* [Getting Started - Console Application](./getting-started-console/README.md)
* [Logging with Complex Objects](./complex-objects/README.md)

## Logging API

### ILogger

.NET supports high performance, structured logging via the
[`Microsoft.Extensions.Logging.ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
interface (including
[`ILogger<TCategoryName>`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1))
to help monitor application behavior and diagnose issues.

#### Package Version

:heavy_check_mark: You should always use the
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
interface (including
[`ILogger<TCategoryName>`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1))
from the latest stable version of
[Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)
package, regardless of the .NET runtime version being used:

* If you are using the latest stable version of [OpenTelemetry .NET
  SDK](../../src/OpenTelemetry/README.md), you do not have to worry about the
  version of `Microsoft.Extensions.Logging` package because it is already taken
  care of for you via [package dependency](../../Directory.Packages.props).
* Starting from version `3.1.0`, the .NET runtime team is holding a high bar for
  backward compatibility on `Microsoft.Extensions.Logging` even during major
  version bumps, so compatibility is not a concern here.

#### Get Logger

In order to use the `ILogger` interface, you need to first get a logger. How to
get a logger depends on two things:

* The type of application you are building.
* The place where you want to log.

Here is the rule of thumb:

* If you are building an application with [dependency injection
  (DI)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
  (e.g. [ASP.NET Core](https://learn.microsoft.com/aspnet/core) and [.NET
  Worker](https://learn.microsoft.com/dotnet/core/extensions/workers)), in most
  cases you should use the logger provided by DI, there are special cases when
  you want log before DI logging pipeline is available or after DI logging
  pipeline is disposed. Refer to the [.NET official
  document](https://learn.microsoft.com/dotnet/core/extensions/logging#integration-with-hosts-and-dependency-injection)
  and [Getting Started with OpenTelemetry .NET Logs in 5 Minutes - ASP.NET Core
  Application](./getting-started-aspnetcore/README.md) tutorial to learn more.
* If you are building an application without DI, create a
  [LoggerFactory](#loggerfactory) instance and configure OpenTelemetry to work
  with it. Refer to the [Getting Started with OpenTelemetry .NET Logs in 5
  Minutes - Console Application](./getting-started-console/README.md) tutorial
  to learn more.

:heavy_check_mark: You should use dot-separated
[UpperCamelCase](https://en.wikipedia.org/wiki/Camel_case) as the log category
name, which makes it convenient to [filter logs](#log-filtering). A common
practice is to use fully qualified class name, and if further categorization is
desired, append a subcategory name. Refer to the [.NET official
document](https://learn.microsoft.com/dotnet/core/extensions/logging#log-category)
to learn more.

```csharp
loggerFactory.CreateLogger<MyClass>(); // this is equivalent to CreateLogger("MyProduct.MyLibrary.MyClass")
loggerFactory.CreateLogger("MyProduct.MyLibrary.MyClass"); // use the fully qualified class name
loggerFactory.CreateLogger("MyProduct.MyLibrary.MyClass.DatabaseOperations"); // append a subcategory name
loggerFactory.CreateLogger("MyProduct.MyLibrary.MyClass.FileOperations"); // append another subcategory name
```

:stop_sign: You should avoid creating loggers too frequently. Although loggers
are not super expensive, they still come with CPU and memory cost, and are meant
to be reused throughout the application. Refer to the [logging performance
benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

#### Write log messages

:heavy_check_mark: You should use structured logging.

* Structured logging is more efficient than unstructured logging.
  * Filtering and redaction can happen on individual key-value pairs instead of
    the entire log message.
  * Storage and indexing are more efficient.
* Structured logging makes it easier to manage and consume logs.

```csharp
var food = "tomato";
var price = 2.99;

logger.LogInformation("Hello from {food} {price}.", food, price);
```

:stop_sign: You should avoid string interpolation.

> [!WARNING]
> The following code has bad performance due to [string
  interpolation](https://learn.microsoft.com/dotnet/csharp/tutorials/string-interpolation):

```csharp
var food = "tomato";
var price = 2.99;

logger.LogInformation($"Hello from {food} {price}.");
```

Refer to the [logging performance
benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

:heavy_check_mark: You should use [compile-time logging source
generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
pattern to achieve the best performance.

```csharp
var food = "tomato";
var price = 2.99;

logger.SayHello(food, price);

internal static partial class LoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from {food} {price}.")]
    public static partial void SayHello(this ILogger logger, string food, double price);
}
```

> [!NOTE]
> There is no need to pass in an explicit
  [EventId](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.eventid)
  while using
  [LoggerMessageAttribute](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggermessageattribute).
  A durable `EventId` will be automatically assigned based on the hash of the
  method name during code generation.

:heavy_check_mark: You can use
[LogPropertiesAttribute](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.logpropertiesattribute)
from
[Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions/)
if you need to log complex objects. Check out the [Logging with Complex
Objects](./complex-objects/README.md) tutorial for more details.

:stop_sign: You should avoid the extension methods from
[LoggerExtensions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggerextensions),
these methods are not optimized for performance.

> [!WARNING]
> The following code has bad performance due to
  [boxing](https://learn.microsoft.com/dotnet/csharp/programming-guide/types/boxing-and-unboxing):

```csharp
var food = "tomato";
var price = 2.99;

logger.LogInformation("Hello from {food} {price}.", food, price);
```

Refer to the [logging performance
benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

:heavy_check_mark: You should hold a high bar while using
[`ILogger.IsEnabled`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger.isenabled).

The logging API is highly optimized for the scenario where most loggers are
**disabled** for certain log levels. Making an extra call to `IsEnabled` before
logging will not give you any performance gain.

> [!WARNING]
> The `logger.IsEnabled(LogLevel.Information)` call in the following code is not
  going to give any performance gain. Refer to the [logging performance
  benchmark](../../test/Benchmarks/Logs/LogBenchmarks.cs) for more details.

```csharp
var food = "tomato";
var price = 2.99;

if (logger.IsEnabled(LogLevel.Information)) // do not do this, there is no perf gain
{
    logger.SayHello(food, price);
}

internal static partial class LoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from {food} {price}.")]
    public static partial void SayHello(this ILogger logger, string food, double price);
}
```

`IsEnabled` can give performance benefits when it is expensive to evaluate the
arguments. For example, in the following code the `Database.GetFoodPrice`
invocation will be skipped if the logger is not enabled:

```csharp
if (logger.IsEnabled(LogLevel.Information))
{
    logger.SayHello(food, Database.GetFoodPrice(food));
}
```

Although `IsEnabled` can give some performance benefits in the above scenario,
for most users it can cause more problems. For example, the performance of the
code is now depending on which logger is being enabled, not to mention the
argument evaluation might have significant side effects that are now depending
on the logging configuration.

:heavy_check_mark: You should use a dedicated parameter to log exceptions when
using the compile-time source generator.

```csharp
var food = "tomato";
var price = 2.99;

try
{
    // Execute some logic

    logger.SayHello(food, price);
}
catch (Exception ex)
{
    logger.SayHelloFailure(ex, food, price);
}

internal static partial class LoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from {food} {price}.")]
    public static partial void SayHello(this ILogger logger, string food, double price);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not say hello from {food} {price}.")]
    public static partial void SayHelloFailure(this ILogger logger, Exception exception, string food, double price);
}
```

> [!NOTE]
> When using the compile-time source generator the first `Exception` parameter
> detected is automatically given special handling. It **SHOULD NOT** be part of
> the message template. For details see: [Log method
> anatomy](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator#log-method-anatomy).

:heavy_check_mark: You should use the dedicated overloads to log exceptions when
using the logging extensions methods.

```csharp
var food = "tomato";
var price = 2.99;

try
{
    // Execute some logic

    logger.LogInformation("Hello from {food} {price}.", food, price);
}
catch (Exception ex)
{
    logger.LogError(ex, "Could not say hello from {food} {price}.", food, price);
}
```

:stop_sign: You should avoid adding exception details into the message template.

You want to use the correct `Exception` APIs because the OpenTelemetry
Specification [defines dedicated
attributes](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-logs.md)
for `Exception` details. The following examples show what **NOT** to do. In
these cases the details won't be lost, but the dedicated attributes also won't
be added.

```csharp
var food = "tomato";
var price = 2.99;

try
{
    // Execute some logic

    logger.SayHello(food, price);
}
catch (Exception ex)
{
    logger.SayHelloFailure(food, price, ex.Message);
}

internal static partial class LoggerExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Hello from {food} {price}.")]
    public static partial void SayHello(this ILogger logger, string food, double price);

    // BAD - Exception should not be part of the message template. Use the dedicated parameter.
    [LoggerMessage(Level = LogLevel.Error, Message = "Could not say hello from {food} {price} {message}.")]
    public static partial void SayHelloFailure(this ILogger logger, string food, double price, string message);
}
```

```csharp
var food = "tomato";
var price = 2.99;

try
{
    // Execute some logic

    logger.LogInformation("Hello from {food} {price}.", food, price);
}
catch (Exception ex)
{
    // BAD - Exception should not be part of the message template. Use the dedicated parameter.
    logger.LogError("Could not say hello from {food} {price} {message}.", food, price, ex.Message);
}
```

## LoggerFactory

In many cases, you can use [ILogger](#ilogger) without having to interact with
[Microsoft.Extensions.Logging.LoggerFactory](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggerfactory)
directly. This section is intended for users who need to create and manage
`LoggerFactory` explicitly.

:stop_sign: You should avoid creating `LoggerFactory` instances too frequently,
`LoggerFactory` is fairly expensive and meant to be reused throughout the
application. For most applications, one `LoggerFactory` instance per process
would be sufficient.

:heavy_check_mark: You should properly manage the lifecycle of
[LoggerFactory](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loggerfactory)
instances if they are created by you.

* If you forget to dispose the `LoggerFactory` instance before the application
  ends, logs might get dropped due to the lack of proper flush.
* If you dispose the `LoggerFactory` instance too early, any subsequent logging
  API invocation associated with the logger factory could become no-op (i.e. no
  logs will be emitted).

## Log Correlation

In OpenTelemetry, logs are automatically correlated to
[traces](../trace/README.md). Check the [Log
Correlation](./correlation/README.md) tutorial to learn more.

## Log Enrichment

TBD

## Log Filtering

The [Customizing OpenTelemetry .NET SDK for
Logs](./customizing-the-sdk/README.md#log-filtering) document has provided
instructions for basic filtering based on logger category name and severity
level.

For more advanced filtering and sampling, the .NET team has a plan to cover it
in .NET 9 timeframe, please use this [runtime
issue](https://github.com/dotnet/runtime/issues/82465) to track the progress or
provide feedback and suggestions.

## Log Redaction

Logs might contain sensitive information such as passwords and credit card
numbers, proper redaction is required to prevent privacy and security incidents.
Check the [Log Redaction](./redaction/README.md) tutorial to learn more.
