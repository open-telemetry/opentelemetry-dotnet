# OpenTelemetry .NET Logs

## Best Practices

The following tutorials have demonstrated the best practices for logging with
OpenTelemetry .NET:

* [Getting Started - Console Application](./getting-started-console/README.md)
* [Getting Started - ASP.NET Core
  Application](./getting-started-aspnetcore/README.md)
* [Logging with Complex Objects](./complex-objects/README.md)

## Structured Logging

:heavy_check_mark: You should use structured logging with OpenTelemetry .NET.

* Structured logging is more efficient than unstructured logging.
  * Filtering and redaction can happen on invidual key-value pairs instead of
    the entire log message.
  * Storage and indexing are more efficient.
* Structured logging makes it easier to manage and consume logs.

:stop_sign: Avoid string interpolation.

> [!WARNING]
> The following code has bad performance due to string interpolation:

```csharp
var name = "tomato";
var price = 2.99;

logger.LogInformation($"Hello from {name} {price}.");
```

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

Use [compile-time logging source
generation](https://docs.microsoft.com/dotnet/core/extensions/logger-message-generator)
pattern to achieve the best performance.

* When using `ILogger`
  [scopes](https://learn.microsoft.com/dotnet/core/extensions/logging#log-scopes),
  use a `List<KeyValuePair<string, object?>>` or `IReadOnlyList<KeyValue<string,
  object?>>` as the state for best performance. Use an
  `IEnumerable<KeyValue<string, object?>>` when performance is *NOT* a critical
  requirement.

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

### Use TagList accordingly

There are two different ways of passing tags to an instrument API:

* Pass the tags directly to the instrument API:

  ```csharp
  counter.Add(100, ("Key1", "Value1"), ("Key2", "Value2"));
  ```

* Use
  [`TagList`](https://learn.microsoft.com/dotnet/api/system.diagnostics.taglist):

  ```csharp
  var tags = new TagList
  {
      { "DimName1", "DimValue1" },
      { "DimName2", "DimValue2" },
      { "DimName3", "DimValue3" },
      { "DimName4", "DimValue4" },
  };

  counter.Add(100, tags);
  ```

Here is the rule of thumb:

* When reporting measurements with 3 tags or less, pass the tags directly to the
  instrument API.
* When reporting measurements with 4 to 8 tags (inclusive), use
  [`TagList`](https://learn.microsoft.com/dotnet/api/system.diagnostics.taglist?#remarks)
  to avoid heap allocation if avoiding GC pressure is a primary performance
  goal. For high performance code which consider reducing CPU utilization more
  important (e.g. to reduce latency, to save battery, etc.) than optimizing
  memory allocations, use profiler and stress test to determine which approach
  is better.
  Here are some [metrics benchmark
  results](../../test/Benchmarks/Metrics/MetricsBenchmarks.cs) for reference.
* When reporting measurements with more than 8 tags, the two approaches share
  very similar CPU performance and heap allocation. `TagList` is recommended due
  to its better readability and maintainability.

> [!NOTE]
> When reporting measurements with more than 8 tags, the API allocates memory on
the hot-path. You SHOULD try to keep the number of tags less than or equal to 8.
If you are exceeding this, check if you can model some of the tags as Resource,
as [shown here](#modeling-static-tags-as-resource).

### Modeling static tags as Resource

Tags such as `MachineName`, `Environment` etc. which are static throughout the
process lifetime should be be modeled as `Resource`, instead of adding them to
each metric measurement. Refer to this
[doc](./customizing-the-sdk/README.md#resource) for details and examples.

## Common issues that lead to missing metrics

* The `Meter` used to create the instruments is not added to the
  `MeterProvider`. Use `AddMeter` method to enable the processing for the
  required metrics.
* Instrument name is invalid. When naming instruments, ensure that the name you
  choose meets the criteria defined in the
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument-name-syntax).
  A few notable characters that are not allowed in the instrument name: `/`
  (forward slash), `\` (backward slash), any space character in the name.
* MetricPoint limit is reached. By default, the SDK limits the number of maximum
  MetricPoints (unique combination of keys and values for a given Metric stream)
  to `2000`. This limit can be configured using
  `SetMaxMetricPointsPerMetricStream` method. Refer to this
  [doc](../../docs/metrics/customizing-the-sdk/README.md#changing-maximum-metricpoints-per-metricstream)
  for more information. The SDK would not process any newer unique key-value
  combination that it encounters, once this limit is reached.
* MeterProvider is disposed. You need to ensure that the `MeterProvider`
  instance is kept active for metrics to be collected. In a typical application,
  a single MeterProvider is built at application startup, and is disposed of at
  application shutdown. For an ASP.NET Core application, use `AddOpenTelemetry`
  and `WithMetrics` methods from the `OpenTelemetry.Extensions.Hosting` package
  to correctly setup `MeterProvider`. Here's a [sample ASP.NET Core
  app](../../examples/AspNetCore/Program.cs) for reference. For simpler
  applications such as Console apps, refer to this
  [example](../../docs/metrics/getting-started-console/Program.cs).
