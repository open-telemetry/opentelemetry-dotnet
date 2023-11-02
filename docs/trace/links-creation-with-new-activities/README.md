# Creating new root activities that link to an existing activity: A Sample

This sample shows how to create new root activities that
[link](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#links-between-spans)
to an existing activity. This can be useful in a fan-out or batched operation
situation when you want to create a new trace with a new root activity
BEFORE invoking each of the fanned out operations, and at the same time
you want each of these new traces to be linked to the original activity.

To give an example, let's say that:

- Service A receives a request for a customer operation that impacts 1000s of
resources. The term "resource" here means an entity that is managed by this
service and should not be confused with the term "resource" in OpenTelemetry.
- Service A orchestrates this overall operation by fanning out multiple
calls to Service B, with one call for EACH of the impacted resources.
- Let's say the number of spans generated for a single resource operation
is in the order of several thousands of spans.

In the above example, if you used the same trace for the entire flow, then
you would end up with a huge trace with more than million spans. This will
make visualizing and understanding the trace difficult.

Further, it may make it difficult to do programmatic analytics at the
*individual* resource operation level (for each of the 1000s of resource
operations) as there would be no single trace that corresponds to each
of the individual resource operations.

Instead, by creating a new trace with a new root activity before the fanout
call, you get a separate trace for each of the resource operations. In
addition, by using the "span links" functionality in OpenTelemetry, we link
each of these new root activities to the original activity.

This enables more granular visualization and analytics.

## How does this example work?

To be able to create new root activities, we first set the Activity.Current
to null so that we can "de-parent" the new activity from the current activity.

For each of the fanned out operations, this creates a new root activity. As
part of this activity creation, it links it to the previously current activity.

Finally, we reset Activity.Current to the previous activity now after we are
done with the fanout. This will ensure that the rest of the code executes
in the context of the original activity.

## When should you consider such an option?  What are the tradeoffs?

This is a good option to consider for operations that involve batched or
fanout operations if using the same trace causes it to become huge.
Using this approach, you can create a new trace for each of the fanned out
operations and link them to the original activity.

A tradeoff is that now we will have multiple traces instead of a single trace.
However, many Observability tools have the ability to visualize linked traces
together, and hence it is not necessarily a concern from that perspective.
However, this model has the potential to add some complexity to any
programmatic analysis since now it has to understand the concept of linked
traces.

## References

- [Links between spans](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#links-between-spans)
- [Creating new root activities](https://opentelemetry.io/docs/instrumentation/net/manual/#creating-new-root-activities)
- [Activity Creation Options](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api#activity-creation-options)
- [A sample where links are used in a fan-in scenario](https://github.com/PacktPublishing/Modern-Distributed-Tracing-in-.NET/tree/main/chapter6/links)

## Sample Output

You should see output such as the below when you run this example. You can see
that EACH of the "fanned out activities" have:

- a new trace ID
- an activity link to the original activity

```text
Activity.TraceId:            5ce4d8ad4926ecdd0084681f46fa38d9
Activity.SpanId:             8f9e9441f0789f6e
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksCreationWithNewRootActivities
Activity.DisplayName:        FannedOutActivity 1
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
Activity.DisplayName:        FannedOutActivity 2
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
Activity.DisplayName:        FannedOutActivity 3
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
Activity.DisplayName:        OrchestratingActivity
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
