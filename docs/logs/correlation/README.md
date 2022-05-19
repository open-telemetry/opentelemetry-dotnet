# Logs correlation

The getting started docs for [logs](../getting-started/README.md) and
[traces](../../trace/getting-started/README.md) showed how to emit logs and
traces independently, and export them to console exporter.

This doc explains how logs can be correlated to traces.

## Logging Data Model support for correlation

[Logging Data
Model](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#trace-context-fields)
defines fields which allow a log to be correlated with span (`Activity` in
.NET). The fields `TraceId` and `SpanId` allow a log to be correlated to
corresponding `Activity`.

## Correlation in OpenTelemetry .NET

The good news is that, in OpenTelemetry .NET SDK, there is no user action
required to enable correlation. i.e the SDK automatically enables logs to
`Activity` correlation, by populating the fields `TraceId`, `SpanId`,
`TraceFlags`, `TraceState` from the active activity (i.e `Activity.Current`), if
any.

The example [Program.cs](./Program.cs) shows how to emit logs within the context
of an active `Activity`. Running the application will show the following output
on the console:

```text
LogRecord.Timestamp:               2022-05-18T18:51:16.4348626Z
LogRecord.TraceId:                 d7aca5b2422ed8d15f56b6a93be4537d
LogRecord.SpanId:                  c90ac2ad41ab4d46
LogRecord.TraceFlags:              Recorded
LogRecord.CategoryName:            Correlation.Program
LogRecord.LogLevel:                Information
LogRecord.State:                   Hello from tomato 2.99.

Resource associated with LogRecord:
service.name: unknown_service:correlation

Activity.TraceId:          d7aca5b2422ed8d15f56b6a93be4537d
Activity.SpanId:           c90ac2ad41ab4d46
Activity.TraceFlags:           Recorded
Activity.ActivitySourceName: MyCompany.MyProduct.MyLibrary
Activity.DisplayName: SayHello
Activity.Kind:        Internal
Activity.StartTime:   2022-05-18T18:51:16.3427411Z
Activity.Duration:    00:00:00.2248932
Activity.Tags:
    foo: 1
Resource associated with Activity:
    service.name: unknown_service:correlation
```

As you can see, the `LogRecord` automatically had the `TraceId`, `SpanId` fields
matching the ones from the `Activity`. In [the logs getting
started](../getting-started/README.md) doc, the logging was done outside of an
`Activity` context, hence these fields in `LogRecord` were not populated.

## Learn more

Check [ASP.NET Core](../../../examples/AspNetCore/README.md) example application
which shows how all the logs done within the context of request are
automatically correlated to the `Activity` representing the incoming request.
