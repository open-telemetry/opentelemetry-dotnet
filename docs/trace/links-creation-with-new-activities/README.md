# Creating new root activities that link to an existing activity: An Example

This example shows how to create new root activities that link to an existing
activity. This can be useful in a fan-out or batched operation situation when
you want to create a new trace with a new root activity before invoking each
of the fanned out operations.

This example shows how to create the new root activities and how to link each
of them to the original activity.

## How does this example work?

To be able to create new root activities, we first set the Activity.Current
to null so that we can "de-parent" the new activity from the current activity.

For each of the fanned out operations, this creates a new root activity. As
part of this activity creation, it links it to the previously current activity.

Finally, we reset Activity.Current to the previous activity now after we are
done with the fanout. This will ensure that the rest of the code executes
in the context of the original activity.

## When should you consider such an option?  What are the tradeoffs?

This is a good option to consider for operations that involve several batched
or fanout operations. Using this approach, you can create a new trace for each
of the fanned out operations and link them to the original activity.

## References

- https://opentelemetry.io/docs/instrumentation/net/manual/#creating-new-root-activities
- https://opentelemetry.io/docs/instrumentation/net/manual/#adding-links

## Sample Output

You should see output such as the below when you run this example.

```text
Activity.TraceId:            5ce4d8ad4926ecdd0084681f46fa38d9
Activity.SpanId:             8f9e9441f0789f6e
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        FannedOutActivity
Activity.Kind:               Internal
Activity.StartTime:          2023-10-17T01:24:40.4957326Z
Activity.Duration:           00:00:00.0008656
Activity.Links:
    2890476acefb53b93af64a0d91939051 16b83c1517629363
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 0.0.0-alpha.0.2600
    service.name: unknown_service:links-creation

Activity.TraceId:            16a8ad23d14a085f2a1f260a4b474d05
Activity.SpanId:             0c3e835cfd60c604
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        FannedOutActivity
Activity.Kind:               Internal
Activity.StartTime:          2023-10-17T01:24:40.5908290Z
Activity.Duration:           00:00:00.0009197
Activity.Links:
    2890476acefb53b93af64a0d91939051 16b83c1517629363
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 0.0.0-alpha.0.2600
    service.name: unknown_service:links-creation

Activity.TraceId:            46f0b5b68173b4acf4f50e1f5cdb3e55
Activity.SpanId:             42e7f4439fc2b416
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        FannedOutActivity
Activity.Kind:               Internal
Activity.StartTime:          2023-10-17T01:24:40.5930378Z
Activity.Duration:           00:00:00.0008622
Activity.Links:
    2890476acefb53b93af64a0d91939051 16b83c1517629363
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 0.0.0-alpha.0.2600
    service.name: unknown_service:links-creation

Activity.TraceId:            2890476acefb53b93af64a0d91939051
Activity.SpanId:             6878c2a84d4d4996
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       16b83c1517629363
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        WrapUp
Activity.Kind:               Internal
Activity.StartTime:          2023-10-17T01:24:40.5950683Z
Activity.Duration:           00:00:00.0008843
Activity.Tags:
    foo: 1
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 0.0.0-alpha.0.2600
    service.name: unknown_service:links-creation

Activity.TraceId:            2890476acefb53b93af64a0d91939051
Activity.SpanId:             16b83c1517629363
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-10-17T01:24:40.4937024Z
Activity.Duration:           00:00:00.1043390
Activity.Tags:
    foo: 1
Resource associated with Activity:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 0.0.0-alpha.0.2600
    service.name: unknown_service:links-creation
```
