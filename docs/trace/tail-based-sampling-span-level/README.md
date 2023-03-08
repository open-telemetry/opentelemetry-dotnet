# Tail Based Sampling at a span level: An Example

This is an example / proof of concept to achieve tail-based sampling at a
span level using the extensibility mechanisms in OpenTelemetry.NET.

This is a way to achieve a combination of:

- Head-based sampling (based on probabilistic sampling) and
- Tail-based sampling to get all failure spans (non-probabilistic sampling).

## How does this sampling example work?

We use a hybrid approach: we do head based sampling to get a
probabilistic subset of all spans which includes both successful spans
and failure spans. In addition, we want to capture all failure spans.
To do this, if the parent based sampler's decision is to drop it, we return
a "Record-Only" sampling result. This ensures that the Span processor
receives that span. In the span processor, at the end of a span, we check if
it is a failure span. If so, we change the decision from "Record-Only"
to set the sampled flag so that the exporter receives the span.
In this example, each span is filtered individually without consideration to any
other spans.

This is a basic form of tail-based sampling at a span level. If a span failed,
we always sample it in addition to all head-sampled spans.

## When should you consider such an option?

This is a good option if you want to get all failure spans in addition to
head based sampling. With this, you get basic span level tail-based sampling
at a SDK level without having to install any additional components.

## Tradeoffs

Tail-sampling this way involves many tradeoffs such as:

1. Additional memory cost: Unlike head-based sampling where the sampling
decision is made at span creation time, in tail sampling the decision is made
only at the end, so there is additional memory cost.

2. Partial traces: Since this sampling is at a span level, the generated trace
will be partial. For example, if another part of the call tree is successful,
those spans may not be exported leading to an incomplete trace.

3. If multiple exporters are used, this decision will impact all of them:
[Issue 3861](https://github.com/open-telemetry/opentelemetry-dotnet/issues/3861).

## Sample Output

You should see output such as the below when you run this example.

```text
Including error span with id
00-404ddff248b8f9a9b21e347d68d2640e-035858bc3c168885-01 and status Error
Activity.TraceId:            404ddff248b8f9a9b21e347d68d2640e
Activity.SpanId:             035858bc3c168885
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.5563112Z
Activity.Duration:           00:00:00.0028144
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Dropping span with id 00-ea861bda268c58d328ab7cbe49851499-daba29055de80a53-00
and status Ok

Including error span with id
00-802dea991247e2d699d943167eb546de-cc120b0bd1741b52-01 and status Error
Activity.TraceId:            802dea991247e2d699d943167eb546de
Activity.SpanId:             cc120b0bd1741b52
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.7021138Z
Activity.Duration:           00:00:00.0000012
Activity.Tags:
    foo: bar
StatusCode: Error
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel

Including head-sampled span with id
00-f3c88010615e285c8f3cb3e2bcd70c7f-f9316215f12437c3-01 and status Ok
Activity.TraceId:            f3c88010615e285c8f3cb3e2bcd70c7f
Activity.SpanId:             f9316215f12437c3
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: SDK.TailSampling.POC
Activity.DisplayName:        SayHello
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T19:05:32.8519346Z
Activity.Duration:           00:00:00.0000034
Activity.Tags:
    foo: bar
StatusCode: Ok
Resource associated with Activity:
    service.name: unknown_service:Examples.TailBasedSamplingAtSpanLevel
```
