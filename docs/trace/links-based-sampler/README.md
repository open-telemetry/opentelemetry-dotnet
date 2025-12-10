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

Now, let's say that the producing activity S1 in trace T1 is sampled, say using
the decision of a parent based sampler. Now, let's say that the activity S2 in trace
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
activity. Let's say that the sampling decision for activity S3 was to drop it.
Now, since S2 in trace T2 links to S1 in trace T1, with this approach S2 will
be sampled (based on the linked context). Hence, the produced trace T2 will be
a partial trace as it will not include activity S3 but will include activity S2.

- **Can lead to higher volume of data**: Since this approach will sample in
activities even if one of the linked activities is sampled, it can lead to higher
volumes of data, as compared to regular head based sampling. This is because
we are making a non-probabilistic sampling decision here based on the sampling
decisions of linked activities. For example, if there are 20 linked activities and
even if only one of them is sampled, then the linking activity will be sampled.

## Sample Output

You should see output such as the below when you run this example.

```text
af448bc1cb3e5be4e4b56a8b6650785c: ParentBasedSampler decision: Drop
af448bc1cb3e5be4e4b56a8b6650785c: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

1b08120fa35c3f4a37e0b6326dc7688c: ParentBasedSampler decision: Drop
1b08120fa35c3f4a37e0b6326dc7688c: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

ff710bd70baf2e8e843e7b38d1fc4cc1: ParentBasedSampler decision: RecordAndSample
Activity.TraceId:            ff710bd70baf2e8e843e7b38d1fc4cc1
Activity.SpanId:             620d9b218afbf926
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-04-18T16:52:16.0373932Z
Activity.Duration:           00:00:00.0022481
Activity.Tags:
    foo: bar
Activity.Links:
    f7464f714b23713c9008f8fc884fc391 7d1c96a6f2c95556
    6660db8951e10644f63cd385e7b9549e 526e615b7a70121a
    4c94df8e520b32ff25fc44e0c8063c81 8080d0aaafa641af
    70d8ba08181b5ec073ec8b5db778c41f 99ea6162257046ab
    d96954e9e76835f442f62eece3066be4 ae9332547b80f50f
Resource associated with Activity:
    service.name: unknown_service:links-sampler


68121534d69b2248c4816c0c5281f908: ParentBasedSampler decision: Drop
68121534d69b2248c4816c0c5281f908: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

5042f2c52a08143f5f42be3818eb41fa: ParentBasedSampler decision: Drop
5042f2c52a08143f5f42be3818eb41fa: At least one linked activity
(TraceID: 5c1185c94f56ebe3c2ccb4b9880afb17, SpanID: 1f77abf0bded4ab9) is sampled.
Hence, LinksBasedSampler decision is RecordAndSample

Activity.TraceId:            5042f2c52a08143f5f42be3818eb41fa
Activity.SpanId:             0f8a9bfa9d7770e6
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-04-18T16:52:16.0806081Z
Activity.Duration:           00:00:00.0018874
Activity.Tags:
    foo: bar
Activity.Links:
    ed77487f4a646399aea5effc818d8bfa fcdde951f29a13e0
    f79860fdfb949f2c1f1698d1ed8036b9 e422cb771057bf7c
    6326338d0c0cf3afe7c5946d648b94dc affc7a6c013ea273
    c0750a9fa146062083b55227ac965ad4 b09d59ed3129779d
    5c1185c94f56ebe3c2ccb4b9880afb17 1f77abf0bded4ab9
Resource associated with Activity:
    service.name: unknown_service:links-sampler


568a2b9489c58e7a5a769d264a9ddf28: ParentBasedSampler decision: Drop
568a2b9489c58e7a5a769d264a9ddf28: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

4f8d972b0d7727821ce4a307a7be8e8f: ParentBasedSampler decision: Drop
4f8d972b0d7727821ce4a307a7be8e8f: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

ce940241ed33e1a030da3e9d201101d3: ParentBasedSampler decision: Drop
ce940241ed33e1a030da3e9d201101d3: At least one linked activity
(TraceID: ba0d91887309399029719e2a71a12f62, SpanID: 61aafe295913080f) is sampled.
Hence, LinksBasedSampler decision is RecordAndSample

Activity.TraceId:            ce940241ed33e1a030da3e9d201101d3
Activity.SpanId:             5cf3d63926ce4fd5
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-04-18T16:52:16.1127688Z
Activity.Duration:           00:00:00.0021072
Activity.Tags:
    foo: bar
Activity.Links:
    5223cff39311c741ef50aca58e4270c3 e401b6840acebf43
    398b43fee8a75b068cdd11018ef528b0 24cfa4d5fb310b9d
    34351a0f492d65ef92ca0db3238f5146 5c0a56a16291d765
    ba0d91887309399029719e2a71a12f62 61aafe295913080f
    de18a8af2d20972cd4f9439fcd51e909 4c40bc6037e58bf9
Resource associated with Activity:
    service.name: unknown_service:links-sampler


ac46618da4495897bacd7d399e6fc6d8: ParentBasedSampler decision: Drop
ac46618da4495897bacd7d399e6fc6d8: No linked span is sampled. Hence,
LinksBasedSampler decision is Drop.

68a3a05e0348d2a2c1c3db34bc3fd2f5: ParentBasedSampler decision: Drop
68a3a05e0348d2a2c1c3db34bc3fd2f5: At least one linked activity
(TraceID: 87773d89fba942b0109d6ce0876bb67e, SpanID: 2aaac98d4e48c261) is sampled.
Hence, LinksBasedSampler decision is RecordAndSample

Activity.TraceId:            68a3a05e0348d2a2c1c3db34bc3fd2f5
Activity.SpanId:             3d0222f56b0e1e5d
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: LinksAndParentBasedSampler.Example
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-04-18T16:52:16.1553354Z
Activity.Duration:           00:00:00.0049821
Activity.Tags:
    foo: bar
Activity.Links:
    7175fbd18da2783dc594d1e8f3260c74 13019d9a06a5505b
    59c9bdd52eb5cf23eae9001006743fcf 25573e0f1b290b8d
    87773d89fba942b0109d6ce0876bb67e 2aaac98d4e48c261
    0a1f65c47f556336b4028b515d363810 0816a2a2b7d4ea0b
    7602375d3eae7e849a9dc27e858dc1c2 b918787b895b1374
Resource associated with Activity:
    service.name: unknown_service:links-sampler
```
