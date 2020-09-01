# Correlate Logs with Traces

Starting from `Microsoft.Extensions.Logging` version `5.0`, logs can be
correlated with distributed tracing by enriching each log entry with the
information from the enclosing `Activity`. This can be achieved by enabling the
`ActivityTrackingOptions`:

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.Configure(options => options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId);
});
```

`Microsoft.Extensions.Logging.ActivityTrackingOptions` supports `TraceId`,
`SpanId`, `ParentId`, `TraceFlags` and `TraceState`.

Please refer to the example [here](./Program.cs).

## References

* [ILogger](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
* [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)
