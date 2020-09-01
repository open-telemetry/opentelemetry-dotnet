# Correlate Logs with Traces

Logs from `ILogger` can be correlated with distributed tracing by enriching each
log entry with the `TraceId` and `SpanId` from the enclosing `Activity`.

Please refer to the example [here](./Program.cs).

## References

* [ILogger](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
