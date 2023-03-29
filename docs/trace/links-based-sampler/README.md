# Links Based Sampling: An Example

Certain scenarios such as a producer consumer scenario can be modelled using
"span links" to express causality between activities. An activity (span) in a trace
can link to any number of activities in other traces. When using a Parent Based
sampler, the sampling decision is made at the level of a single trace. This implies
that the sampling decision across such linked traces is taken independently without
any consideration to the links. This can result in incomplete information to reason
about a system. Ideally, it would be desirable to sample all linked traces together.

As one possible way to address this, this example shows how we can increase the
likelihood of having complete traces across linked traces.

## How does this sampling example work?

We use a composite sampler that makes use of two samplers:

1. A parent based sampler.
2. A links based sampler.

This composite sampler first delegates to the parent based sampler. If the
parent based sampler decides to sample, then the composite sampler decides
to sample. However, if the parent based sampler decides to drop, the composite
sampler delegates to the links based sampler. The links based sampler decides
to sample if the activity has any linked activities and if at least ONE of those
linked activities is sampled.

The links based sampler is not a probabilistic sampler. It is a biased sampler
that decides to sample an activity if any of the linked contexts are sampled.

## When should you consider such an option?  What are the tradeoffs?

This may be a good option to consider if you want to get more complete traces
across linked traces. However, there are a few tradeoffs to consider:

- **Not guaranteed to give consistent sampling in all situations**: This
approach doesn't guarantee that you will get complete traces across linked
traces in all situations.

Let's look at a couple of cases using the same producer-consumer example
scenario. Let's say we have a producer activity (say with ID S1 in Trace T1) that
produces a message and a consumer activity (say with ID S2 in Trace T2)  that
consumes the message.

Now, let's say that the producing activity S1 in trace T1 is sampled, say using the
decision of a parent based sampler. Now, let's say that the activity S2 in trace
T2 is not sampled based on the parent based sampler decision for T2. However,
since this activity S2 in T2 is linked to the producing activity (S1 in T1) that
is sampled, this mechanism ensures that the consuming activity (S2 in T2) will
also get sampled.

Alternatively, let's consider what happens if the producing activity S1 in
trace T1 is not sampled, say using the decision of a parent based sampler.
Now, let's say that the consuming activity S2 in trace T2 is sampled, based
on the decision of a parent based sampler. In this case, we can see that
activity S2 in trace T2 is sampled even though activity S1 in trace T1 is not
sampled. This is an example of a situation where this approach is not helpful.

Another example of a situation where you would get a partial trace is if the
consuming activity S2 in trace T2 is not the root activity in trace T2. In this
case, let's say there's a different activity S3 in trace T2 that is the root
activity. Let's say that the sampling decision was activity S3 was to drop it.
Now, since S2 in trace T2 links to S1 in trace T1, with this approach S2 will
be sampled (based on the linked context). Hence, the produced trace T2 will be
a partial trace.

- **Can lead to higher volume of data**: Since this approach will sample in
activities even if one of the linked activities is sampled, it can lead to higher
volumes of data, as compared to regular head based sampling. This is because
we are making a non-probabilistic sampling decision here based on the sampling
decisions of linked activities. For example, if there are 20 linked activities and
even if only one of them is sampled, then the linking activity will be sampled.

## Sample Output

You should see output such as the below when you run this example.

```text
b712cde6aa4eaa9927f6106e17861d3c: ParentBasedSampler decision: Drop
b712cde6aa4eaa9927f6106e17861d3c: No linked span is sampled.
LinksBasedSampler decision: Drop.

72b00993686449357a94c2bfd536b2ed: ParentBasedSampler decision: Drop
72b00993686449357a94c2bfd536b2ed: No linked span is sampled.
LinksBasedSampler decision: Drop.

5773bf1e0e9b6c4f5f9f561f01e8f394: ParentBasedSampler decision: Drop
5773bf1e0e9b6c4f5f9f561f01e8f394: No linked span is sampled.
LinksBasedSampler decision: Drop.

41b7c1da65927a82e237607748537760: ParentBasedSampler decision: Drop
41b7c1da65927a82e237607748537760: No linked span is sampled.
LinksBasedSampler decision: Drop.

0920a3042929efb8685afa50ef5cd5c3: ParentBasedSampler decision: RecordAndSample
Activity.TraceId:            0920a3042929efb8685afa50ef5cd5c3
Activity.SpanId:             71c413f0cf2a600b
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-03-29T16:54:25.3783462Z
Activity.Duration:           00:00:00.0026831
Activity.Tags:
    foo: bar
Activity.Links:
    7a15435bce073e8d0645789cd0198586 f132ac6be68a13fa
    89f9d15ae85c8b5a511d499c8d024074 b2e73501bf4c9e22
    9694ca9bf89078eb5a0b7a3909107bf3 42626464ce8f3d62
    f9ec03f16e7157c88e7e2369129fcd7b 3b2e89fd24d37c56
    665ab563e79df56914eefaa31465a61b d608bef51d5cecc0
Resource associated with Activity:
    service.name: unknown_service:links-sampler


9b42320a559005ed3358203e507fbf73: ParentBasedSampler decision: Drop
9b42320a559005ed3358203e507fbf73: At least one linked activity is sampled.
LinksBasedSampler decision: RecordAndSample
Activity.TraceId:            9b42320a559005ed3358203e507fbf73
Activity.SpanId:             41a7afd45e589e21
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-03-29T16:54:25.5890651Z
Activity.Duration:           00:00:00.0023307
Activity.Tags:
    foo: bar
Activity.Links:
    294922daedd20f74a65eeae8e86e64ca 835e800d0e0cb9ed
    e65e146c15d4e65c0a2958d537b5ee2d 7217651b841464c6
    d56f7cbee9d9e85142e2395cd3410a65 248bc3bf50c42d37
    438ffb7a2fbfcbd0f2c0c548734b3377 707b0ed41f815013
    f36092671286f42d5ef422df5003a3b4 4df103c2567c4153
Resource associated with Activity:
    service.name: unknown_service:links-sampler


8d8a37097bc698eddbf0f74f87c2bb68: ParentBasedSampler decision: Drop
8d8a37097bc698eddbf0f74f87c2bb68: No linked span is sampled.
LinksBasedSampler decision: Drop.

b640159ee5b28da8e1022d65c46ccf97: ParentBasedSampler decision: Drop
b640159ee5b28da8e1022d65c46ccf97: No linked span is sampled.
LinksBasedSampler decision: Drop.

2240cb1d683a5467d7bb3f41e8d96fa0: ParentBasedSampler decision: Drop
2240cb1d683a5467d7bb3f41e8d96fa0: At least one linked activity is sampled.
LinksBasedSampler decision: RecordAndSample
Activity.TraceId:            2240cb1d683a5467d7bb3f41e8d96fa0
Activity.SpanId:             99946d9145fbe367
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-03-29T16:54:25.6265057Z
Activity.Duration:           00:00:00.0029163
Activity.Tags:
    foo: bar
Activity.Links:
    e97146f1bdf3fa7fd0e85c292ab28514 0946c50c4b3fe52e
    aa7b20ff019e19f38627b5bb659f7fc4 720e2a83df885796
    c952dd79e1199ea9b1dcb5dedd33a92c 05e38253e40c452f
    636c5e3ca771ed1766ebbd0a675b773d baf119ff34903b24
    d38a81cb579cb4e91091045818941b5d 9686feedc52b6fe2
Resource associated with Activity:
    service.name: unknown_service:links-sampler


37ac3dc5296c6ab155b71c3cb457b286: ParentBasedSampler decision: Drop
37ac3dc5296c6ab155b71c3cb457b286: No linked span is sampled.
LinksBasedSampler decision: Drop.
```
