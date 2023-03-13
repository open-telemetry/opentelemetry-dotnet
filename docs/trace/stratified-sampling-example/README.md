# Stratified Sampling: An Example

This example shows one possible way to achieve stratified sampling in
OpenTelemetry.NET.

## What is stratified sampling?

Stratified sampling is a way to divide a population into mutually exclusive
sub-populations or "strata". For example, the strata for a population of
"queries" could be "user-initiated queries" and "programmatic queries". Each
stratum is then sampled using a probabilistic sampling method. This ensures
that all sub-populations are represented.

## How does this example do stratified sampling?

We achieve this by using a custom Sampler that internally holds two samplers.
Based on the stratum, the appropriate sampler is invoked.

One prerequisite for this is that the tag (e.g. queryType) used for the
stratified sampling decision must be provided as part of activity creation.

We use disproportionate stratified sampling (also known as "unequal probability
sampling") here - i.e., the sample size of each sub-population is not
proportionate to their occurrence in the overall population. In this example,
we want to ensure that all user initiated queries are represented, so we use a
100% sampling rate for it, while the sampling rate chosen for programmatic
queries is much lower.

## What is an example output?

You should see the following output on the Console when you use "dotnet run" to
run this application. This shows that the two sub-populations (strata) are being
sampled independently.

```text
StratifiedSampler handling userinitiated query
Activity.TraceId:            1a122d63e5f8d32cb8ebd3e402eb5389
Activity.SpanId:             83bdc6bbebea1df8
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       1ddd00d845ad645e
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8156879Z
Activity.Duration:           00:00:00.0008656
Activity.Tags:
    queryType: userInitiated
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            1a122d63e5f8d32cb8ebd3e402eb5389
Activity.SpanId:             1ddd00d845ad645e
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8115186Z
Activity.Duration:           00:00:00.0424036
Activity.Tags:
    queryType: userInitiated
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
Activity.TraceId:            03cddefbc0e0f61851135f814522a2df
Activity.SpanId:             8d4fa3e27a12f666
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       8c46e4dc6d0f418c
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8553756Z
Activity.Duration:           00:00:00.0000019
Activity.Tags:
    queryType: programmatic
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling userinitiated query
Activity.TraceId:            8a5894524f1bea2a7bd8271fef9ec22d
Activity.SpanId:             94b5b004287bd678
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       99600e9fe011c1cc
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9660777Z
Activity.Duration:           00:00:00.0000005
Activity.Tags:
    queryType: userInitiated
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType
```
