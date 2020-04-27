# Using OpenTelemetry with Application Insights

## Create Application Insights resource
If you don't have resource yet [create one](https://docs.microsoft.com/en-us/azure/azure-monitor/app/create-new-resource). 

## Setup for applications with dependency injection 

If you have ASP.NET Core web application or worker service, or if you simply use use `Microsoft.Extensions.DependencyInjection`,
just call `AddOpenTelemetry` during host builder configuration in `ConfigureServices` (in `Startup` or `Program`).

1. Install packages (latest version)
``` xml
    <PackageReference Include="OpenTelemetry" Version="0.2.0-alpha.182" />
    <PackageReference Include="OpenTelemetry.Adapter.AspNetCore" Version="0.2.0-alpha.182" />
    <PackageReference Include="OpenTelemetry.Adapter.Dependencies" Version="0.2.0-alpha.182" />
    <PackageReference Include="OpenTelemetry.Exporter.ApplicationInsights" Version="0.2.0-alpha.182" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="0.2.0-alpha.182" />
```

2. Set up OpenTelemetry
```csharp
services.AddOpenTelemetry((sp, builder) =>
{
    builder
        .SetResource(Resources.CreateServiceResource("my-service"))   // set any service name as you would like to appear on the Application Map
        .UseApplicationInsights(telemetryConfiguration =>
        {
            telemetryConfiguration.InstrumentationKey = Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey");
        })
        .AddRequestAdapter() // regirsters ASP.NET Core incoming requests tracking 
        .AddDependencyAdapter(); // regirsters outgoing requests tracking
});
```

## Applications without dependency injection

1. Install OpenTelemetry packages (latest version)

``` xml
<PackageReference Include="OpenTelemetry" Version="0.2.0-alpha.182" />
<PackageReference Include="OpenTelemetry.Adapter.Dependencies" Version="0.2.0-alpha.182" />
<PackageReference Include="OpenTelemetry.Exporter.ApplicationInsights" Version="0.2.0-alpha.182" />
```

2. Create `TraceFactory` and make sure to keep it alive during application lifetime.
Avoid creating multiple `TracerFactories` and dispose factory before application exits.

```csharp
var tracerFactory = TracerFactory.Create(builder => 
    builder
        .SetResource(Resources.CreateServiceResource("my-service"))   // set any service name as you would like to appear on the Application Map
        .UseApplicationInsights(telemetryConfiguration =>
        {
            telemetryConfiguration.InstrumentationKey = "your instrumentation key";
        })
        .AddDependencyAdapter(); // regirsters outgoing requests tracking

                var tracer = tracerFactory.GetTracer("http-client-test");

// start root span
using (tracer.StartActiveSpan("root span", out _))
{
    // do stuff, start other spans, etc 
}

// dispose before exit to flush all spans
tracerFactory.Dispose();
```

### Export exception and logs from ILogger

OpenTelemetry does not support logging yet, but you can export your logs to Application Insights.
Here is [more info](https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger) on ILogger support with Application Insights.

Logs will be correlated to the OpenTelemetry spans.

1. In addition to OpenTelemetry packages, install  `Microsoft.Extensions.Logging.ApplicationInsights` (latest).
```xml
<PackageReference Include="Microsoft.Extensions.Logging.ApplicationInsights" Version="2.12.1" />
```

2. Configure logging per [documentation](https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger).

Here is the full example:

```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        //....
        .ConfigureServices((context, services) =>
        {
            var instrumentationKey = context.Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey");

            // set up OpenTelemetry
            services.AddOpenTelemetry(b => b
                .SetResource(Resources.CreateServiceResource("line-counter")) // use unique name 
                .UseApplicationInsights(o => o.InstrumentationKey = instrumentationKey)
                .AddDependencyAdapter()
                .AddRequestAdapter());

            // set up correlation between spans and logs
            services.Configure<TelemetryConfiguration>(telemetryConfiguration =>
            {
                telemetryConfiguration.TelemetryInitializers.Add(new OperationCorrelationTelemetryInitializer());
            });
        })
        .ConfigureLogging((context, builder) =>
        {
            var instrumentationKey = context.Configuration.GetValue<string>("ApplicationInsights:InstrumentationKey");

            // configure Application Insights provider for ILogger
            builder.AddApplicationInsights(instrumentationKey);
        });
```

