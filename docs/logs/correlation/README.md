# Correlate Logs with Traces

Starting from `Microsoft.Extensions.Logging` version `5.0`, logs can be
correlated with distributed tracing by enriching each log entry with the
information from the enclosing `Activity`. This can be achieved by enabling the
`ActivityTrackingOptions`. In a [non-host console
app](https://docs.microsoft.com/aspnet/core/fundamentals/logging#non-host-console-app),
it can be achieved as shown below.

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.Configure(options => options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId);
});
```

Please refer to the example [here](./Program.cs).

In an ASP.NET Core app, the above can be achieved by modifying the host
building, as shown below.

```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureLogging(loggingBuilder =>
            loggingBuilder.Configure(options =>
                options.ActivityTrackingOptions =
                    ActivityTrackingOptions.TraceId
                    | ActivityTrackingOptions.SpanId))
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```

`Microsoft.Extensions.Logging.ActivityTrackingOptions` supports `TraceId`,
`SpanId`, `ParentId`, `TraceFlags` and `TraceState`.

## References

* [ILogger](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
* [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)
