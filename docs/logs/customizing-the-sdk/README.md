# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

TODO

## Building the OpenTelemetryLoggerProvider

TODO

## OpenTelemetryLoggerProvider configuration

TODO

### IncludeScopes

A "scope" is an ILogger concept that can group a set of logical operations and
attach data to each log created as part of a set.

`IncludeScopes` will include all scopes with the exported `LogRecord`.

The snippet below demonstrates `IncludeScopes = true` using the
`ConsoleExporter`:

```
using(logger.BeginScope("My Scope 1"))
using(logger.BeginScope("My Scope 2"))
{
    logger.LogInformation("Hello Information within scope");
}
```

```
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
LogRecord.EventId:            0
LogRecord.EventName:
LogRecord.CategoryName:       Program
LogRecord.LogLevel:           Information
LogRecord.TraceFlags:         None
LogRecord.State:              Hello Information within scope
LogRecord.ScopeValues (Key:Value):
[Scope.0]:                              My Scope 1
[Scope.1]:                              My Scope 2
```

See [Program.cs](Program.cs) for complete example.

### IncludeFormattedMessage

TODO

### ParseStateValues

TODO

### AddProcessor

TODO

### SetResourceBuilder

TODO

## Filtering LogLevels

TODO

### via appsettings.json

TODO

### via code

TODO

## Learn more

* TODO
