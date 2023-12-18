# OpenTelemetry .NET Traces

## Best Practices

### ActivitySource should be singleton

`ActivitySource` SHOULD only be created once and reused throughout the
application lifetime. This
[example](./getting-started-console/Program.cs) shows how
`ActivitySource` is created as a `static` field and then used in the
application. You could also look at this ASP.NET Core
[example](../../examples/AspNetCore/Program.cs) which shows a more Dependency
Injection friendly way of doing this by extracting the `ActivitySource` into a
dedicated class called
[Instrumentation](../../examples/AspNetCore/Instrumentation.cs) which is then
added as a `Singleton` service.

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

- The `ActivitySource` used to create the `Activity` is not added to the
  `TracerProvider`. Use `AddSource` method to enable the activity from a given
  `ActivitySource`.
- `TracerProvider` is disposed too early. You need to ensure that the
  `TracerProvider` instance is kept active for traces to be collected. In a
  typical application, a single TracerProvider is built at application startup,
  and is disposed of at application shutdown. For an ASP.NET Core application,
  use `AddOpenTelemetry` and `WithTraces` methods from the
  `OpenTelemetry.Extensions.Hosting` package to correctly setup
  `TracerProvider`. Here's a [sample ASP.NET Core
  app](../../examples/AspNetCore/Program.cs) for reference. For simpler
  applications such as Console apps, refer to this
  [example](../../docs/trace/getting-started-console/Program.cs).
- TODO: Sampling
