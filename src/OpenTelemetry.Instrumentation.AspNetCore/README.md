# ASP.NET Core Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)

Automatically instruments the incoming requests to [ASP.NET
Core](https://docs.microsoft.com/en-us/aspnet/core).
This includes incoming gRPC requests using
[Grpc.AspNetCore](https://www.nuget.org/packages/Grpc.AspNetCore).

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

## Enable AspNetCore Instrumentation

`OpenTelemetry.Instrumentation.AspNetCore` must be enabled at application
startup, typically in the `ConfigureServices` method of your ASP.NET Core
application's `Startup.cs` class, as shown below.

```csharp
services.AddOpenTelemetryTracerProvider(
    (builder) => builder
                .AddAspNetCoreInstrumentation()
                );
```

## Filtering

TODO.

## Enriching automaticaly collected activity with additional information

This instrumentation library stores the raw `HttpRequest`, `HttpResponse`
objects in the activity. This can be accessed in ActivityProcessors, and
can be used to further enrich the Activity with additional tags as shown
below.

```csharp
internal class MyAspNetCoreEnrichingProcessor : ActivityProcessor
{
    public override void OnStart(Activity activity)
    {
        // Retrieve the HttpRequest object.
        var httpRequest = activity.GetCustomProperty("OTel.AspNetCore.Request")
                          as HttpRequest;
        if (httpRequest != null)
        {
            // Add more tags to the activity
            activity.SetTag("mycustomtag", httpRequest.Headers["myheader"]);
        }
    }

    public override void OnEnd(Activity activity)
    {
        // Retrieve the HttpResponse object.
        var httpResponse = activity.GetCustomProperty("OTel.AspNetCore.Response")
                           as HttpResponse;
        if (httpResponse != null)
        {
            var statusCode = httpResponse.StatusCode;
            bool success = statusCode < 400;
            // Add more tags to the activity or replace an existing tag.
            activity.SetTag("myCustomSuccess", success);
        }
    }
}
```

The custom processor must be added to the provider as below.

```csharp
services.AddOpenTelemetryTracerProvider(
    (builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddProcessor(new MyAspNetCoreEnrichingProcessor())
                );
```

## References

* [Introduction to ASP.NET
  Core](https://docs.microsoft.com/aspnet/core/introduction-to-aspnet-core)
* [OpenTelemetry Project](https://opentelemetry.io/)
