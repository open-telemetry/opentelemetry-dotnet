---
title: "Getting Started"
weight: 2
---

OpenTelemetry for .NET is unique among OpenTelemetry implementations, as it is integrated with the .NET `System.Diagnostics` library. At a high level, you can think of OpenTelemetry for .NET as a bridge between the telemetry available through `System.Diagnostics` and the greater OpenTelemetry ecosystem, such as OpenTelemetry Protocol (OTLP) and the OpenTelemetry Collector. 

# Installation

You can find OpenTelemetry packages on [NuGet](https://www.nuget.org/profiles/OpenTelemetry). Install them to your project file using the `dotnet` command line utility or through `PackageReference` statements in your `csproj` file.

# Initialization and Configuration

OpenTelemetry should be configured as part of your services initialization. In ASP.NET Core, you'll want to add it to the `IServiceCollection` that is created in `public void ConfigureServices(IServiceCollection services)`, in `Startup.cs`. For ASP.NET, configuration occurs in `Global.asax.cs`.

You can find a variety of code samples demonstrating how to initialize and configure OpenTelemetry for .NET [here](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples)

## Creating a Tracer Provider

In order to create and process traces, a tracer provider must be created. We'll look at two ways to do this - one for a console application, and one for ASP.NET Core. The biggest difference you should note here is that if you're using ASP.NET, then the OpenTelemetry libraries will automatically register with the `ActivitySource` provided by the framework, meaning you don't need to create and manage activity sources yourself.

### Console

First, you'll need to declare an `ActivitySource` for the tracer provider to read from.

```
private static readonly ActivitySource MyActivitySource = new ActivitySource("MySource");
```

Then, inside your main function, initialize a provider:

```
var tracerProvider = Sdk.CreateTracerProviderBuilder()
  .SetSampler(new AlwaysOnSampler())
  .AddSource("MySource")
  .AddConsoleExporter()
  .Build();
```

### ASP.NET Core

In `Startup.cs`, add a new service to your `IServiceCollection`:

```
public void ConfigureServices(IServiceCollection services)
{
  // other configuration here...
  services.AddOpenTelemetryTracing((builder) => builder
    .AddAspNetCoreInstrumentation()
    .AddConsoleExporter());
}
```

## Creating a Console Exporter

The console exporter doesn't require any special configuration, however, you can pass a `ConsoleExporterOptions` object to it in order to set the destination (either stdout or debug console). See [the exporter page in GitHub](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.Console) for details.

# Quick Start

Putting it together, a simple example of creating traces in a console application is as follows:

```
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
            activity?.SetTag("baz", new int[] { 1, 2, 3 });
        }
    }
}
```

You can find a quick start example of an ASP.NET Core application [here](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/AspNetCore)