# OpenTelemetry .NET Traces

<!-- markdownlint-disable MD033 -->
<details>
<summary>Table of Contents</summary>

* [Best Practices](#best-practices)
* [Package Version](#package-version)
* [Tracing API](#tracing-api)
* [TracerProvider Management](#tracerprovider-management)
* [Correlation](#correlation)

</details>
<!-- markdownlint-enable MD033 -->

## Best Practices

The following tutorials have demonstrated the best practices for while using
traces with OpenTelemetry .NET:

* [Getting Started - ASP.NET Core
  Application](./getting-started-aspnetcore/README.md)
* [Getting Started - Console Application](./getting-started-console/README.md)

## Package Version

:heavy_check_mark: You should always use the
[System.Diagnostics.Activity](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity)
APIs from the latest stable version of
[System.Diagnostics.DiagnosticSource](https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/)
package, regardless of the .NET runtime version being used:

* If you're using the latest stable version of [OpenTelemetry .NET
  SDK](../../src/OpenTelemetry/README.md), you don't have to worry about the
  version of `System.Diagnostics.DiagnosticSource` package because it is already
  taken care of for you via [package
  dependency](../../Directory.Packages.props).
* The .NET runtime team is holding a high bar for backward compatibility on
  `System.Diagnostics.DiagnosticSource` even during major version bumps, so
  compatibility is not a concern here.

## Tracing API

:stop_sign: You should avoid creating
[`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource)
too frequently. `ActivitySource` is fairly expensive and meant to be reused
throughout the application. For most applications, it can be modeled as static
readonly field (e.g. [Program.cs](./getting-started-console/Program.cs)) or
singleton via dependency injection (e.g.
[Instrumentation.cs](../../examples/AspNetCore/Instrumentation.cs)).

:heavy_check_mark: You should use dot-separated
[UpperCamelCase](https://en.wikipedia.org/wiki/Camel_case) as the
[`ActivitySource.Name`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource.name).
In many cases, using the fully qualified class name might be a good option.

```csharp
static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");
```

:heavy_check_mark: You should check
[`Activity.IsAllDataRequested`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.isalldatarequested)
before [setting
Tags](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.settag)
for better performance.

```csharp
using (var activity = MyActivitySource.StartActivity("SayHello"))
{
    if (activity != null && activity.IsAllDataRequested == true)
    {
        activity.SetTag("http.url", "http://www.mywebsite.com");
    }
}
```

:heavy_check_mark: You should use
[Activity.SetTag](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.settag)
instead of
[Activity.AddTag](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.addtag)
and
[Activity.SetCustomProperty](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.setcustomproperty)
because the latter do not perform deduplication.

## TracerProvider Management

:stop_sign: You should avoid creating `TracerProvider` instances too frequently,
`TracerProvider` is fairly expensive and meant to be reused throughout the
application. For most applications, one `TracerProvider` instance per process
would be sufficient.

:heavy_check_mark: You should properly manage the lifecycle of `TracerProvider`
instances if they are created by you.

Here is the rule of thumb when managing the lifecycle of `TracerProvider`:

* If you are building an application with [dependency injection
  (DI)](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
  (e.g. [ASP.NET Core](https://learn.microsoft.com/aspnet/core) and [.NET
  Worker](https://learn.microsoft.com/dotnet/core/extensions/workers)), in most
  cases you should create the `TracerProvider` instance and let DI manage its
  lifecycle. Refer to the [Getting Started with OpenTelemetry .NET Traces in 5
  Minutes - ASP.NET Core Application](./getting-started-aspnetcore/README.md)
  tutorial to learn more.
* If you are building an application without DI, create a `TracerProvider`
  instance and manage the lifecycle explicitly. Refer to the [Getting Started
  with OpenTelemetry .NET Traces in 5 Minutes - Console
  Application](./getting-started-console/README.md) tutorial to learn more.
* If you forget to dispose the `TracerProvider` instance before the application
  ends, traces might get dropped due to the lack of proper flush.
* If you dispose the `TracerProvider` instance too early, any subsequent
  activities will not be collected.

## Correlation

In OpenTelemetry, traces are automatically [correlated to
logs](../logs/README.md#log-correlation) and can be [correlated to
metrics](../metrics/README.md#metrics-correlation) via
[exemplars](../metrics/exemplars/README.md).

### Manually creating Activities

As shown in the [getting started](getting-started-console/README.md) guide, it
is very easy to manually create `Activity`. Due to this, it can be tempting to
create too many activities (eg: for each method call). In addition to being
expensive, excessive activities can also make trace visualization harder.
Instead of manually creating `Activity`, check if you can leverage
instrumentation libraries, such as [ASP.NET
Core](../../src/OpenTelemetry.Instrumentation.AspNetCore/README.md),
[HttpClient](../../src/OpenTelemetry.Instrumentation.Http/README.md) which will
not only create and populate `Activity` with tags(attributes), but also take
care of propagating/restoring the context across process boundaries. If the
`Activity` produced by the instrumentation library is missing some information
you need, it is generally recommended to enrich the existing Activity with that
information, as opposed to creating a new one.

### Modelling static tags as Resource

Tags such as `MachineName`, `Environment` etc. which are static throughout the
process lifetime should be be modelled as `Resource`, instead of adding them
to each `Activity`. Refer to this
[doc](./customizing-the-sdk/README.md#resource) for details and
examples.

## Common issues that lead to missing traces

* The `ActivitySource` used to create the `Activity` is not added to the
  `TracerProvider`. Use `AddSource` method to enable the activity from a given
  `ActivitySource`.
* `TracerProvider` is disposed too early. You need to ensure that the
  `TracerProvider` instance is kept active for traces to be collected. In a
  typical application, a single TracerProvider is built at application startup,
  and is disposed of at application shutdown. For an ASP.NET Core application,
  use `AddOpenTelemetry` and `WithTraces` methods from the
  `OpenTelemetry.Extensions.Hosting` package to correctly setup
  `TracerProvider`. Here's a [sample ASP.NET Core
  app](../../examples/AspNetCore/Program.cs) for reference. For simpler
  applications such as Console apps, refer to this
  [example](../../docs/trace/getting-started-console/Program.cs).
* TODO: Sampling
