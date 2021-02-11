# Overview

The goal of this Grocery example is to try and instrument the code. From this
excersize we hope to discover and learn additional topics for discussions.

We are focus only on the API side at the moment.  It is known that SDK implementation
will likely affect how the API is designed, but we will make our best judgement to
tolerate the situation at this time.

## Topics for discussions

- Need some kind of concrete LabelSet() in API.  Access to LabelSetSdk() is unavailable 
  from API side.

- It is inconvenient to have to pass a default(SpanContext) when we don't care about spans.
  Need additional prototypes to make SpanContext optional.

- We create instrument with CreateInt64Counter() but it returns a generic Counter<long>. 
  Seems like it should return a Int64Counter instead.

- It's not allowed to new MeterProvider().  Thus, the only way to access is via the Default property.
  We can probably simplify MeterProvider.Default.GetMeter()
  to simply MeterProvider.GetMeter().

- Bound counters does not allow adding more labels.  Ideally, we would bind Store,
  but still allow passing in additional lables (i.e. Customer) when recording measurements.

- Need shutdown() for SDK
