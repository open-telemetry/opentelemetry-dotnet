# Log Correlation

The getting started docs for [logs](../getting-started-console/README.md) and
[traces](../../trace/getting-started-console/README.md) showed how to emit logs
and traces independently, and export them to console exporter.

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
`Activity` correlation, by populating the fields `TraceId`, `SpanId` and
`TraceFlags` from the active activity (i.e `Activity.Current`), if
any.

The example [Program.cs](./Program.cs) shows how to emit logs within the context
of an active `Activity`. Running the application will show the following output
on the console:

```text
LogRecord.Timestamp:               2024-01-26T17:55:39.2273475Z
LogRecord.TraceId:                 aed89c3b250fb9d8e16ccab1a4a9bbb5
LogRecord.SpanId:                  bd44308753200c58
LogRecord.TraceFlags:              Recorded
LogRecord.CategoryName:            Program
LogRecord.Severity:                Info
LogRecord.SeverityText:            Information
LogRecord.Body:                    Food `{name}` price changed to `{price}`.
LogRecord.Attributes (Key:Value):
    name: artichoke
    price: 9.99
    OriginalFormat (a.k.a Body): Food `{name}` price changed to `{price}`.
LogRecord.EventId:                 344095174
LogRecord.EventName:               FoodPriceChanged

...

Activity.TraceId:            aed89c3b250fb9d8e16ccab1a4a9bbb5
Activity.SpanId:             bd44308753200c58
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: MyCompany.MyProduct.MyLibrary
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2024-01-26T17:55:39.2223849Z
Activity.Duration:           00:00:00.0361682
...
```

As you can see, the `LogRecord` automatically had the `TraceId`, `SpanId` fields
matching the ones from the `Activity`. In [the logs getting
started](../getting-started-console/README.md) doc, the logging was done outside
of an `Activity` context, hence these fields in `LogRecord` were not populated.

## Learn more

Check [ASP.NET Core](../../../examples/AspNetCore/README.md) example application
which shows how all the logs done within the context of request are
automatically correlated to the `Activity` representing the incoming request.
