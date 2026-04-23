# Dynamic Activity Subscription in OpenTelemetry .NET

## Problem Statement

The OpenTelemetry .NET SDK provides `AddSource` and `AddInstrumentation` APIs on
`TracerProviderBuilder` to configure which `ActivitySource` instances a
`TracerProvider` listens to. Once the provider is built, **there is no mechanism
to add, remove, or modify source subscriptions**. The only recourse is to
dispose the entire `TracerProvider` and rebuild it from scratch, which disrupts
in-flight traces and requires re-initialization of the processor pipeline,
exporters, and instrumentations.

This document explores the constraints imposed by the `System.Diagnostics`
runtime, evaluates possible designs for dynamic subscription within the current
runtime, and proposes a runtime API enhancement that would provide a clean
solution.

---

## Background: How Activity Subscription Works Today

### Runtime Layer (`System.Diagnostics`)

The .NET runtime provides two types involved in activity subscription:

- **`ActivitySource`** - Created by library authors to emit activities (spans).
  Each source has a `Name` and `Version`.
- **`ActivityListener`** - Created by consumers (the OTel SDK) to receive
  activities. Registered globally via `ActivitySource.AddActivityListener()`.

Subscription is established through two delegate callbacks on
`ActivityListener`:

```text
┌──────────────────────────────────────────────────────────┐
│                     ActivityListener                      │
│                                                          │
│  ShouldListenTo: Func<ActivitySource, bool>              │
│    - Evaluated ONCE per source-listener pair              │
│    - Called when listener is registered (against all      │
│      existing sources) or when a new source is created    │
│    - Result is CACHED — never re-evaluated                │
│                                                          │
│  Sample: SampleActivity<ActivityContext>                  │
│    - Evaluated on EVERY ActivitySource.StartActivity()    │
│    - Called only for sources where ShouldListenTo = true  │
│    - Returns ActivitySamplingResult:                      │
│        None              → Activity NOT created (zero     │
│                            allocation)                    │
│        PropagationData   → Minimal activity for context   │
│                            propagation                    │
│        AllData           → Full activity, not recorded    │
│        AllDataAndRecorded → Full activity, recorded       │
│                                                          │
│  ActivityStarted: Action<Activity>                       │
│    - Called after Activity is created                     │
│    - Used for processor pipeline OnStart                  │
│                                                          │
│  ActivityStopped: Action<Activity>                       │
│    - Called when Activity.Stop() is invoked               │
│    - Used for processor pipeline OnEnd                    │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

**The critical constraint:** `ShouldListenTo` is the only place where the
`ActivitySource` identity is available. It is evaluated once and the result is
cached in the runtime's internal data structures. The `Sample` delegate receives
`ActivityCreationOptions<ActivityContext>` which contains `Name`, `Kind`,
`Parent`, `TraceId`, `Tags`, and `Links` — but **not** the `ActivitySource` that
is creating the activity.

### SDK Layer (OpenTelemetry .NET)

The `TracerProviderSdk` constructor (`TracerProviderSdk.cs:112-282`) configures
a single `ActivityListener`:

1. **Source filtering** (`TracerProviderSdk.cs:246-279`): Source names from
   `AddSource()` calls are baked into either a `HashSet<string>` (exact match)
   or a compiled `Regex` (wildcard patterns via `WildcardHelper`). This becomes
   the `ShouldListenTo` delegate.

2. **Sampling** (`TracerProviderSdk.cs:223-241`): The `Sample` delegate is wired
   to one of three fast paths depending on the configured `Sampler`:
   - `AlwaysOnSampler` → returns `AllDataAndRecorded` (unless suppressed)
   - `AlwaysOffSampler` → returns `PropagateOrIgnoreData` (unless suppressed)
   - Any other sampler → calls `ComputeActivitySamplingResult` which constructs
     `SamplingParameters` and invokes `Sampler.ShouldSample()`

3. **Registration** (`TracerProviderSdk.cs:281-282`):

   ```csharp
   ActivitySource.AddActivityListener(activityListener);
   this.listener = activityListener;
   ```

   After this point, the listener configuration is frozen.

**What CAN be modified after build:**

- Processors can be added via `TracerProvider.AddProcessor()` — the processor
  pipeline uses a linked list (`CompositeProcessor`) that supports append
- `Sdk.SuppressInstrumentation` provides dynamic, per-async-context suppression
  via `AsyncLocal<T>` (but this is a global kill switch, not per-source)

**What CANNOT be modified after build:**

- Source subscriptions (the `ShouldListenTo` predicate)
- The sampler
- Instrumentation registrations

---

## Design Space: Approaches to Dynamic Subscription

### Design 1: Layered Listeners

**Concept:** Instead of a single `ActivityListener`, use one listener per
dynamically-managed source. Adding a source creates and registers a new
listener; removing a source disposes its listener.

```text
┌─────────────────────────────────────────────────────────────┐
│                     TracerProviderSdk                        │
│                                                             │
│  ┌───────────────────┐   ┌────────────────────────────────┐ │
│  │   Core Listener    │   │    DynamicListenerManager      │ │
│  │                    │   │                                │ │
│  │ ShouldListenTo:    │   │  ConcurrentDictionary<string,  │ │
│  │  HashSet{"HttpC",  │   │    ActivityListener>            │ │
│  │   "SqlClient"}     │   │                                │ │
│  │                    │   │  "Redis"  → Listener₁          │ │
│  │ Sample: sampler    │   │  "gRPC"   → Listener₂          │ │
│  │ Started: pipeline  │   │  "Kafka"  → Listener₃          │ │
│  │ Stopped: pipeline  │   │                                │ │
│  └────────┬───────────┘   └───────────┬────────────────────┘ │
│           │                           │                      │
│           └───────────┬───────────────┘                      │
│                       ▼                                      │
│            Shared Processor Pipeline                         │
│       ┌─────────┬──────────┬───────────┐                     │
│       │ Filter  │  Batch   │ Exporter  │                     │
│       └─────────┴──────────┴───────────┘                     │
└─────────────────────────────────────────────────────────────┘
```

#### Implementation Sketch — Design 1

```csharp
public class DynamicTracerProvider : IDisposable
{
    private readonly ConcurrentDictionary<string, ActivityListener> _dynamicListeners = new();
    private readonly Sampler _sampler;
    private readonly BaseProcessor<Activity>? _processor;

    /// <summary>
    /// Subscribes to activities from the specified source.
    /// If already subscribed, this is a no-op.
    /// </summary>
    public void EnableSource(string sourceName)
    {
        if (_dynamicListeners.ContainsKey(sourceName))
            return;

        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                string.Equals(source.Name, sourceName, StringComparison.OrdinalIgnoreCase),

            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                !Sdk.SuppressInstrumentation
                    ? ComputeActivitySamplingResult(ref options, _sampler)
                    : ActivitySamplingResult.None,

            ActivityStarted = activity =>
            {
                if (activity.IsAllDataRequested)
                    _processor?.OnStart(activity);
            },

            ActivityStopped = activity =>
            {
                if (activity.IsAllDataRequested)
                    _processor?.OnEnd(activity);
            },
        };

        if (_dynamicListeners.TryAdd(sourceName, listener))
        {
            ActivitySource.AddActivityListener(listener);
        }
        else
        {
            // Another thread won the race — dispose our listener
            listener.Dispose();
        }
    }

    /// <summary>
    /// Unsubscribes from activities from the specified source.
    /// In-flight activities from this source will complete normally
    /// but no new activities will be created.
    /// </summary>
    public void DisableSource(string sourceName)
    {
        if (_dynamicListeners.TryRemove(sourceName, out var listener))
        {
            listener.Dispose();
        }
    }

    /// <summary>
    /// Returns the set of currently enabled dynamic sources.
    /// </summary>
    public IReadOnlyCollection<string> GetEnabledSources()
        => _dynamicListeners.Keys.ToArray();
}
```

#### Trade-offs — Design 1

| Aspect | Assessment |
| -------- | ------------ |
| **Dynamic add** | Full support — new listener subscribes to existing and future ActivitySources |
| **Dynamic remove** | Full support — disposing listener cleanly unsubscribes |
| **Overhead per source** | Each listener is iterated by the runtime on every `StartActivity()` for any subscribed source. Cost: ~1 delegate invocation per listener per activity creation |
| **Scalability** | Works well for tens of dynamic sources. At hundreds+, the per-`StartActivity` iteration cost becomes significant |
| **Thread safety** | `ConcurrentDictionary` handles concurrent enable/disable. `ActivitySource.AddActivityListener` and `Dispose` are thread-safe in the runtime |
| **Processor pipeline** | All listeners share one pipeline. No duplicate processing |
| **In-flight activities** | Activities started before `DisableSource` will complete normally — the `Activity` object holds its own state, not a reference to the listener |
| **Complexity** | Moderate. Main risk is lifecycle management of many listeners |

#### When to Use — Design 1

- Systems that need to enable/disable specific instrumentation libraries at
  runtime (e.g., enable Redis tracing only during incident investigation)
- Moderate number of dynamic sources (< 50)
- True unsubscription is required (not just sampling suppression)

---

### Design 2: Broad Subscribe with Dynamic Sample Gate

**Concept:** Subscribe to a broad set of potential sources via `ShouldListenTo`,
then dynamically control which sources actually produce activities via the
`Sample` delegate. When `Sample` returns `ActivitySamplingResult.None`, the
runtime does **not** allocate an `Activity` object — this is the cheapest
possible rejection path.

**The Challenge:** The `Sample` delegate receives
`ActivityCreationOptions<ActivityContext>`, which does not expose the
`ActivitySource`. We need the source identity to make per-source decisions.

#### Sub-approach 2a: Per-Source Listeners with Shared Gate

Create one listener per potential source (declared at build time), but all
listeners reference a shared, mutable enabled-set:

```text
┌──────────────────────────────────────────────────────────┐
│                    TracerProviderSdk                      │
│                                                          │
│  Shared State:                                           │
│    _enabledSources: ImmutableHashSet<string>             │
│    (updated atomically via ImmutableInterlocked)          │
│                                                          │
│  Per potential source:                                   │
│  ┌───────────────────────────────────────────────┐       │
│  │ Listener for "HttpClient"                     │       │
│  │  ShouldListenTo: s.Name == "HttpClient"       │       │
│  │  Sample: if !_enabledSources.Contains(        │       │
│  │            "HttpClient") → None               │       │
│  │          else → normal sampling               │       │
│  └───────────────────────────────────────────────┘       │
│  ┌───────────────────────────────────────────────┐       │
│  │ Listener for "SqlClient"                      │       │
│  │  ShouldListenTo: s.Name == "SqlClient"        │       │
│  │  Sample: if !_enabledSources.Contains(        │       │
│  │            "SqlClient") → None                │       │
│  │          else → normal sampling               │       │
│  └───────────────────────────────────────────────┘       │
│  ... (one per potential source)                          │
│                                                          │
│  Processor Pipeline (shared)                             │
└──────────────────────────────────────────────────────────┘
```

#### Implementation Sketch — Design 2a

```csharp
public class GatedTracerProvider : IDisposable
{
    private ImmutableHashSet<string> _enabledSources;
    private readonly List<ActivityListener> _listeners = [];
    private readonly Sampler _sampler;
    private readonly BaseProcessor<Activity>? _processor;

    internal void Initialize(IEnumerable<string> potentialSources, IEnumerable<string> initiallyEnabled)
    {
        _enabledSources = initiallyEnabled
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceName in potentialSources)
        {
            var captured = sourceName; // closure capture

            var listener = new ActivityListener
            {
                ShouldListenTo = source =>
                    string.Equals(source.Name, captured, StringComparison.OrdinalIgnoreCase),

                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    if (Sdk.SuppressInstrumentation)
                        return ActivitySamplingResult.None;

                    // Dynamic gate: check if this source is currently enabled
                    if (!_enabledSources.Contains(captured))
                        return ActivitySamplingResult.None;

                    return ComputeActivitySamplingResult(ref options, _sampler);
                },

                ActivityStarted = activity =>
                {
                    if (activity.IsAllDataRequested)
                        _processor?.OnStart(activity);
                },

                ActivityStopped = activity =>
                {
                    if (activity.IsAllDataRequested)
                        _processor?.OnEnd(activity);
                },
            };

            _listeners.Add(listener);
            ActivitySource.AddActivityListener(listener);
        }
    }

    /// <summary>
    /// Enables activity collection from the specified source.
    /// The source must have been declared as a potential source at build time.
    /// Takes effect on the next StartActivity call — zero allocation overhead
    /// when disabled (Sample returns None).
    /// </summary>
    public void EnableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Add(name),
            sourceName);
    }

    /// <summary>
    /// Disables activity collection from the specified source.
    /// In-flight activities will complete normally. New StartActivity calls
    /// will return null (no Activity allocated).
    /// </summary>
    public void DisableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Remove(name),
            sourceName);
    }

    public IReadOnlyCollection<string> GetEnabledSources()
        => _enabledSources;
}
```

#### Sub-approach 2b: Single Listener (Requires Runtime Change)

If `ActivityCreationOptions<ActivityContext>` exposed the `ActivitySource` (see
[Runtime Proposal](#runtime-api-proposal) below), a single listener would
suffice:

```csharp
// Hypothetical — requires ActivityCreationOptions.Source to be public
var enabledSources = ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

listener.ShouldListenTo = _ => true; // subscribe to everything

listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
{
    if (Sdk.SuppressInstrumentation)
        return ActivitySamplingResult.None;

    // Dynamic gate using source identity
    if (!enabledSources.Contains(options.Source.Name))
        return ActivitySamplingResult.None;

    return ComputeActivitySamplingResult(ref options, sampler);
};
```

#### Trade-offs — Design 2

| Aspect | 2a (Per-Source Listeners) | 2b (Single Listener) |
| -------- | -------------------------- | ---------------------- |
| **Dynamic enable/disable** | Yes — `Sample` checks mutable set | Yes — same |
| **Dynamic add new sources** | No — potential sources fixed at build | Yes — `ShouldListenTo = _ => true` |
| **Overhead when disabled** | Near-zero — `Sample` returns `None` before any allocation | Same |
| **Listener count** | N (one per potential source) | 1 |
| **Runtime iteration cost** | O(N) per `StartActivity` | O(1) |
| **Requires runtime change** | No | Yes (`ActivityCreationOptions.Source`) |
| **Unsubscription** | No true unsubscribe — listener stays registered, just returns `None` | Same |

#### Performance Characteristics

The key insight is the cost of `ActivitySamplingResult.None`:

```text
ActivitySource.StartActivity("operation")
  └→ For each subscribed listener:
       └→ Call listener.Sample(ref options)
            └→ Returns None
  └→ All listeners returned None → return null (no Activity allocated)
```

When `Sample` returns `None`, the total overhead is:

1. The `StartActivity` method entry
2. One `HashSet.Contains` lookup per subscribed listener
3. The method return

No `Activity` object is allocated. No `ActivityStarted` callback fires. This is
as cheap as it gets without true unsubscription.

#### When to Use — Design 2

- The set of potential sources is known at build time (2a) or is unbounded (2b)
- High-frequency enable/disable toggling is needed
- Performance is critical — zero allocation for disabled sources
- True unsubscription is not required (phantom listeners are acceptable)

---

### Design 3: Listener Recycling with Overlap Window

**Concept:** When the source set changes, build a new `ActivityListener` with
the updated configuration, register it, then dispose the old one. The overlap
window ensures no activities are lost.

```text
Timeline:
───────────────────────────────────────────────────────────►
     │                    │              │
     │  Old Listener      │  Both Active │  New Listener
     │  (sources A,B)     │  (overlap)   │  (sources A,C)
     │                    │              │
     ├─ new registered ───┤              │
     │                    ├─ old disposed ┤
```

#### Implementation Sketch — Design 3

```csharp
public class RecyclableTracerProvider : IDisposable
{
    private ActivityListener? _listener;
    private readonly object _recycleLock = new();
    private readonly Sampler _sampler;
    private readonly BaseProcessor<Activity>? _processor;

    /// <summary>
    /// Atomically updates the set of sources being listened to.
    /// During the transition, both old and new listeners are briefly active.
    /// </summary>
    /// <remarks>
    /// Callers should be aware of the following during the overlap window:
    /// <list type="bullet">
    /// <item>Activities from sources in BOTH old and new sets may trigger
    /// duplicate OnStart/OnEnd callbacks</item>
    /// <item>Activities from sources ONLY in the old set will complete on
    /// the old listener's callbacks</item>
    /// <item>Activities from sources ONLY in the new set will be picked up
    /// by the new listener</item>
    /// </list>
    /// The overlap window is typically sub-millisecond.
    /// </remarks>
    public void UpdateSources(IReadOnlyCollection<string> newSources)
    {
        var newListener = BuildListener(newSources);

        lock (_recycleLock)
        {
            // 1. Register new listener FIRST
            //    From this point, activities flow through both listeners
            ActivitySource.AddActivityListener(newListener);

            // 2. Swap the reference
            var old = Interlocked.Exchange(ref _listener, newListener);

            // 3. Dispose old listener
            //    In-flight activities already have their Activity objects;
            //    they will complete normally. The ActivityStopped callback
            //    on the old listener will still fire for those activities.
            old?.Dispose();
        }
    }

    private ActivityListener BuildListener(IReadOnlyCollection<string> sources)
    {
        var sourceSet = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);

        return new ActivityListener
        {
            ShouldListenTo = source => sourceSet.Contains(source.Name),

            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                !Sdk.SuppressInstrumentation
                    ? ComputeActivitySamplingResult(ref options, _sampler)
                    : ActivitySamplingResult.None,

            ActivityStarted = activity =>
            {
                if (activity.IsAllDataRequested)
                    _processor?.OnStart(activity);
            },

            ActivityStopped = activity =>
            {
                if (activity.IsAllDataRequested)
                    _processor?.OnEnd(activity);
            },
        };
    }
}
```

#### Overlap Window Analysis

The overlap window creates specific edge cases:

**Duplicate callbacks:** An activity from a source present in both old and new
sets could receive `ActivityStarted`/`ActivityStopped` from both listeners. This
means the processor pipeline sees the same `Activity` twice.

Mitigation strategies:

1. **Idempotent processors**: Design processors to handle duplicate calls (e.g.,
   check a flag on the Activity)
2. **Dedup processor**: Insert a processor that tracks Activity IDs and
   deduplicates
3. **Dispose-first order**: Dispose old before registering new. This creates a
   brief gap (missed activities) instead of duplicates. For many use cases,
   missing a few activities is preferable to duplicating them.

```csharp
// Gap strategy (miss activities instead of duplicating)
public void UpdateSourcesWithGap(IReadOnlyCollection<string> newSources)
{
    var newListener = BuildListener(newSources);

    lock (_recycleLock)
    {
        var old = Interlocked.Exchange(ref _listener, newListener);
        old?.Dispose();                                    // gap starts
        ActivitySource.AddActivityListener(newListener);   // gap ends
    }
}
```

#### Trade-offs — Design 3

| Aspect | Assessment |
| -------- | ------------ |
| **Dynamic add/remove** | Full support — complete reconfiguration |
| **Listener count** | Always 1 (briefly 2 during transition) |
| **Overhead** | None for removed sources (fully unsubscribed) |
| **Transition safety** | Brief overlap/gap window with edge cases |
| **Complexity** | Low — straightforward rebuild |
| **Change frequency** | Best for infrequent changes (< 1/second) |
| **Atomicity** | Not atomic — brief window of inconsistency |

#### When to Use — Design 3

- Configuration changes are infrequent (e.g., driven by config file reload)
- The source set changes completely (not just incremental add/remove)
- Simplicity is valued over transition-window correctness
- Acceptable to briefly miss or duplicate a small number of activities

---

### Design 4: Filtering Processor

**Concept:** Subscribe broadly and implement dynamic filtering as a processor in
the pipeline. Unlike the `Sample`-level gate in Design 2, this operates after
the `Activity` has been created.

```text
ActivitySource.StartActivity()
  └→ ShouldListenTo: _ => true (or broad pattern)
  └→ Sample: AlwaysOn → Activity CREATED
  └→ ActivityStarted callback
       └→ DynamicFilterProcessor.OnStart(activity)
            └→ Check if activity.Source.Name is enabled
            └→ If not: set activity.IsAllDataRequested = false
                       (downstream processors/exporters skip it)
  └→ ActivityStopped callback
       └→ DynamicFilterProcessor.OnEnd(activity)
            └→ If not enabled: return (don't forward to exporter)
```

#### Implementation Sketch — Design 4

```csharp
/// <summary>
/// A processor that dynamically filters activities based on their source.
/// Activities from disabled sources are marked as not-requested, preventing
/// downstream processors and exporters from processing them.
/// </summary>
public sealed class DynamicSourceFilterProcessor : BaseProcessor<Activity>
{
    private ImmutableHashSet<string> _enabledSources;

    public DynamicSourceFilterProcessor(IEnumerable<string> initialSources)
    {
        _enabledSources = initialSources
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void EnableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Add(name),
            sourceName);
    }

    public void DisableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Remove(name),
            sourceName);
    }

    public override void OnStart(Activity data)
    {
        if (!_enabledSources.Contains(data.Source.Name))
        {
            // Mark as not requested — downstream processors should respect this
            data.IsAllDataRequested = false;
        }
    }

    public override void OnEnd(Activity data)
    {
        // Activities with IsAllDataRequested = false are typically
        // skipped by exporters (e.g., SimpleActivityExportProcessor
        // checks this flag before exporting)
    }
}
```

#### Trade-offs — Design 4

| Aspect | Assessment |
| -------- | ------------ |
| **Dynamic control** | Full — enable/disable at any time |
| **Activity allocation** | Activities ARE allocated even for disabled sources. This is the major cost |
| **Integration** | Clean — fits naturally into the processor pipeline, which already supports dynamic addition via `TracerProvider.AddProcessor()` |
| **Source identity** | Available — `Activity.Source` is a public property on the created `Activity` object |
| **Complexity** | Low — standard processor, no listener management |
| **Overhead** | High — every activity from a broad subscription is allocated, populated with tags/links, and flows through the pipeline until filtered |

#### Performance Impact

This is the most expensive approach because filtering happens *after* activity
creation:

```text
Cost comparison per StartActivity call for a DISABLED source:

Design 1 (Layered):    Listener not subscribed → zero cost
Design 2 (Sample gate): Sample → None → zero allocation
Design 3 (Recycle):    Listener not subscribed → zero cost
Design 4 (Processor):  Activity allocated → OnStart called → filtered
                       Memory: ~400-800 bytes per Activity
                       CPU: allocation + GC pressure + pipeline traversal
```

#### When to Use — Design 4

- Prototyping or low-throughput systems where allocation overhead is acceptable
- When you need access to the full `Activity` object (including `Source.Name`)
  to make filtering decisions
- As a complement to other approaches (e.g., coarse `Sample` gate + fine
  processor filter)

---

## Comparison Matrix

| | Design 1: Layered | Design 2a: Sample Gate | Design 2b: Single+Source | Design 3: Recycle | Design 4: Processor |
| --- | --- | --- | --- | --- | --- |
| **Dynamic add** | Yes | No (pre-declared) | Yes | Yes | Yes (if broadly subscribed) |
| **Dynamic remove** | Yes (true unsub) | Yes (gate only) | Yes (gate only) | Yes (true unsub) | Yes (filter only) |
| **Listeners** | N per source | N per potential source | 1 | 1 (rebuilt) | 1 |
| **Disabled source overhead** | Zero | ~Zero (Sample→None) | ~Zero | Zero | High (Activity allocated) |
| **Transition atomicity** | Atomic per source | Atomic (volatile read) | Atomic | Brief gap/overlap | Atomic (volatile read) |
| **Runtime change needed** | No | No | Yes | No | No |
| **Complexity** | Moderate | Moderate | Low | Low | Low |
| **Best for** | True unsub, moderate N | Known sources, perf | Ideal (future) | Infrequent changes | Prototyping |

---

## Trace Propagation and Child Span Impact

The designs above focus on the mechanics of subscribing and unsubscribing from
`ActivitySource` instances, but they do not address a critical consequence:
**what happens to downstream trace context and child spans when a source is
disabled?**

This section analyses the propagation implications of each design and identifies
a fundamental trade-off that any implementation must make explicit.

### How `Activity.Current` Drives the Trace Graph

In .NET, `Activity.Current` is an `AsyncLocal<Activity?>`. When
`ActivitySource.StartActivity()` creates an `Activity`, it is pushed onto the
`Activity.Current` stack. Child operations started within that async scope
automatically parent themselves to `Activity.Current`.

When `StartActivity()` returns `null` — because no listener is subscribed, or
the `Sample` delegate returns `None` — **no `Activity` is pushed**. The
`Activity.Current` reference is unchanged. This has cascading effects on child
spans and context propagation.

```text
Normal flow (source A enabled):
  HTTP Request (Activity A) → Activity.Current = A
    └→ SQL Call (Activity B, parent = A) → Activity.Current = B
         └→ B.ParentId = A.Id, B.TraceId = A.TraceId ✓

Source A disabled (Sample → None, or unsubscribed):
  HTTP Request → StartActivity returns null → Activity.Current UNCHANGED
    └→ SQL Call (Activity B, parent = whatever was current before)
         └→ B is either:
            - A root span (new TraceId) if there was no prior Activity, OR
            - Parented to a grandparent, skipping the disabled span entirely
         └→ The "HTTP Request" gap is invisible — the trace graph has a hole
```

### Per-Design Propagation Analysis

#### Designs 1 & 3 (True Unsubscription) — Context Chain Broken

Disposing a listener (Design 1) or rebuilding with a reduced source set (Design
3) means the runtime genuinely stops calling `Sample` for that source.
`StartActivity()` returns `null`.

- Child spans from **other** subscribed sources lose their parent link to the
  disabled source's span
- If the disabled span was an intermediate operation (e.g., an HTTP client call
  between an incoming request and a database query), the database span either
  becomes a root span or attaches to a grandparent
- **W3C `traceparent` propagation to downstream services is broken** — no
  `Activity` means no context to inject into outgoing HTTP headers

#### Design 2 (Sample Gate → `None`) — Same Effect as Unsubscription

When `Sample` returns `ActivitySamplingResult.None`, the runtime does **not**
create an `Activity`. This is functionally identical to unsubscription from the
child span perspective:

- `StartActivity()` returns `null`
- `Activity.Current` is not updated
- Child spans lose their parent, same as Designs 1 & 3

**Key insight:** returning `None` from `Sample` and disposing the listener
produce the same propagation outcome — a hole in the trace graph.

#### Design 2 (Sample Gate → `PropagationData`) — Context Preserved

An alternative not explored in the original designs: instead of returning `None`
for disabled sources, return `ActivitySamplingResult.PropagationData`:

```csharp
Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
{
    if (Sdk.SuppressInstrumentation)
        return ActivitySamplingResult.None;

    if (!_enabledSources.Contains(captured))
        return ActivitySamplingResult.PropagationData; // NOT None

    return ComputeActivitySamplingResult(ref options, _sampler);
};
```

`PropagationData` creates a lightweight `Activity` with trace ID, span ID, and
parent ID populated, but `IsAllDataRequested` is `false`. This means:

- `Activity.Current` **is** set — child spans maintain correct parentage
- W3C `traceparent` injection into outgoing calls works correctly
- Processors and exporters skip the activity (not recorded)
- Cost: a small `Activity` allocation per disabled-source operation (much less
  than `AllData`, but not zero)

This is exactly what the SDK already does for the `AlwaysOffSampler` path — see
`PropagateOrIgnoreData` in `TracerProviderSdk.cs:510-518`.

#### Design 4 (Processor) — Context Preserved, Highest Cost

The processor approach creates a full `Activity` (sampled as
`AllDataAndRecorded` by the listener), then marks it as not-requested in
`OnStart`:

- `Activity.Current` **is** set — child spans maintain correct parentage
- W3C `traceparent` propagation works
- The `Activity` is allocated with full data, then downstream processors skip it

This preserves the trace graph but at the highest allocation cost.

**Implementation note:** the processor must also clear the `Recorded` trace
flag, not just `IsAllDataRequested`:

```csharp
public override void OnStart(Activity data)
{
    if (!_enabledSources.Contains(data.Source.Name))
    {
        data.IsAllDataRequested = false;
        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
```

### Propagation Comparison

| Design | Disabled Source Cost | `Activity.Current` Set | Child Spans Parented | W3C Context Propagated |
| -------- | --------------------- | ------------------------ | --------------------- | ---------------------- |
| 1 (Layered, dispose) | Zero | No | **Broken** | **Broken** |
| 2 (Sample → `None`) | Zero | No | **Broken** | **Broken** |
| 2 (Sample → `PropagationData`) | Low (~minimal Activity) | Yes | Preserved | Preserved |
| 3 (Recycle) | Zero | No | **Broken** | **Broken** |
| 4 (Processor) | High (full Activity) | Yes | Preserved | Preserved |

### User Expectations vs. Actual Behaviour

The propagation impact creates a gap between what users may expect from a
per-source "disable" switch and what actually happens. There are two
fundamentally different use cases:

#### Use Case 1: "Disable This Source and Its Subtree"

A user wants to stop a noisy or expensive source — for example, a bug in a
release is causing extremely high SQL trace volume and they need to disable it
immediately to control observability costs.

In this scenario, **broken propagation is acceptable and even expected**. The
user is saying "I don't want SQL traces, period." If SQL spans disappear, it is
logical that child spans of those SQL operations (if any) also disappear. This
maps directly to Designs 1, 2 (`None`), or 3 — true unsubscription or `Sample`
returning `None`. The cost savings are maximal (zero allocation) and the trace
graph reflects reality: those operations are not being observed.

This is also the natural behaviour of simply not subscribing to a source at all,
which is what happens when a source is omitted from `AddSource()` at build time.
A dynamic version of this (enabling/disabling at runtime) would logically behave
the same way — the source is treated as if it were never configured.

#### Use Case 2: "Skip This Source's Spans but Keep Everything Else Intact"

A user wants to remove a specific source's spans from export but preserve the
trace graph. For example, a middleware source generates high-volume low-value
spans, but child operations (database calls, downstream HTTP calls) should
remain correctly parented and the trace should be continuous.

This is a **processor-level concern**, not a subscription-level one. The source
must remain subscribed so that `Activity` objects are created and
`Activity.Current` is maintained. Filtering happens later in the pipeline:

- In `OnStart`: mark the activity as not-recorded (prevents enrichment and
  export)
- In `OnEnd`: skip export but allow child activities to complete normally
- **Advanced option:** a processor could theoretically cache child spans and
  re-parent them (setting `ParentId` to the skipped span's parent) before
  export. This would eliminate the skipped span from the exported trace while
  maintaining a continuous parent-child chain. However, this adds significant
  complexity (memory overhead for caching, race conditions with concurrent
  spans, and the need to handle the case where the parent is also skipped). This
  is an area for future exploration rather than an initial implementation.

#### Implication for Dynamic Configuration

The distinction matters for API design. A per-source "disable" toggle needs to
clearly communicate which behaviour it provides:

- **`DisableSource("SqlClient")`** → Designs 1/2/3 behaviour. No activities
  created. Child spans orphaned. Zero overhead. Suitable for cost control and
  emergency suppression.
- **`FilterSource("SqlClient")`** → Design 4 / processor behaviour. Activities
  created but not exported. Trace graph preserved. Higher overhead. Suitable for
  noise reduction while maintaining observability.

These are distinct operations and should not be conflated in the API surface.

---

## Runtime API Proposal

The designs above work within the current `System.Diagnostics` constraints, but
they all involve trade-offs that stem from two fundamental limitations:

1. **`ShouldListenTo` is evaluated once and cached** — there is no way to
   trigger re-evaluation
2. **`ActivityCreationOptions<T>` does not expose the `ActivitySource`** — the
   `Sample` delegate cannot identify which source is creating the activity

We propose two complementary runtime API additions that would unlock clean
dynamic subscription.

### Proposal A: `ActivityListener.Resubscribe()`

#### API Surface — Proposal A

```csharp
namespace System.Diagnostics
{
    public sealed class ActivityListener : IDisposable
    {
        // Existing members...

        /// <summary>
        /// Re-evaluates <see cref="ShouldListenTo"/> against all existing
        /// <see cref="ActivitySource"/> instances, updating subscriptions
        /// to match the current predicate result.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method iterates all live <see cref="ActivitySource"/> instances
        /// and calls <see cref="ShouldListenTo"/> for each one. Sources where
        /// the predicate now returns <c>true</c> (but previously returned
        /// <c>false</c>) are subscribed. Sources where the predicate now returns
        /// <c>false</c> (but previously returned <c>true</c>) are unsubscribed.
        /// </para>
        /// <para>
        /// In-flight activities from unsubscribed sources will complete normally.
        /// Their <see cref="ActivityListener.ActivityStopped"/> callback will
        /// still be invoked.
        /// </para>
        /// <para>
        /// This method is thread-safe and can be called concurrently with
        /// <see cref="ActivitySource.StartActivity(string)"/>.
        /// </para>
        /// </remarks>
        public void Resubscribe();
    }
}
```

#### Usage Pattern

```csharp
var enabledSources = new ConcurrentHashSet<string>(StringComparer.OrdinalIgnoreCase);

var listener = new ActivityListener
{
    ShouldListenTo = source => enabledSources.Contains(source.Name),
    Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
        ComputeSamplingResult(ref options),
    ActivityStarted = activity => processor?.OnStart(activity),
    ActivityStopped = activity => processor?.OnEnd(activity),
};

ActivitySource.AddActivityListener(listener);

// Later: dynamically add a source
enabledSources.Add("MyLibrary.Redis");
listener.Resubscribe(); // re-evaluates against all ActivitySources

// Later: dynamically remove a source
enabledSources.Remove("MyLibrary.Redis");
listener.Resubscribe(); // unsubscribes from Redis source
```

#### Implementation Strategy

The runtime maintains the relationship between `ActivitySource` and
`ActivityListener` in internal data structures. The implementation of
`Resubscribe()` needs to:

1. Iterate all live `ActivitySource` instances
2. For each source, evaluate the current `ShouldListenTo` predicate
3. Add or remove the subscription as needed
4. Handle thread safety with concurrent `StartActivity` calls

Here is a sketch of the runtime implementation, based on the existing patterns
in `System.Diagnostics.DiagnosticSourceEventSource` and `ActivitySource`:

```csharp
// In ActivityListener (System.Diagnostics)
public void Resubscribe()
{
    // The runtime maintains a linked list of all ActivitySource instances.
    // ActivitySource.AddActivityListener already iterates this list to
    // evaluate ShouldListenTo — we reuse the same iteration pattern.

    // Lock ordering: same as AddActivityListener to prevent deadlocks
    lock (ActivitySource.s_activeSources)
    {
        SynchronizedList<ActivitySource>? sources = ActivitySource.s_activeSources;

        for (int i = 0; i < sources.Count; i++)
        {
            ActivitySource source = sources[i];
            bool shouldListen = false;

            try
            {
                shouldListen = ShouldListenTo?.Invoke(source) ?? false;
            }
            catch
            {
                // Predicate threw — treat as "don't listen"
                shouldListen = false;
            }

            bool currentlyListening = source.HasListener(this);

            if (shouldListen && !currentlyListening)
            {
                // New subscription
                source.AddListener(this);
            }
            else if (!shouldListen && currentlyListening)
            {
                // Remove subscription
                // In-flight activities are not affected — they hold
                // their own state and will complete normally
                source.RemoveListener(this);
            }
        }
    }
}
```

#### Runtime Internal Changes Required

The existing `ActivitySource` class would need:

```csharp
public sealed class ActivitySource : IDisposable
{
    // Existing: internal list of listeners subscribed to this source
    private SynchronizedList<ActivityListener>? _listeners;

    // NEW: Check if a specific listener is subscribed
    internal bool HasListener(ActivityListener listener)
    {
        var listeners = _listeners;
        if (listeners == null) return false;

        lock (listeners)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (ReferenceEquals(listeners[i], listener))
                    return true;
            }
        }
        return false;
    }

    // NEW: Remove a specific listener (not dispose — just unsubscribe from this source)
    internal void RemoveListener(ActivityListener listener)
    {
        var listeners = _listeners;
        if (listeners == null) return;

        lock (listeners)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (ReferenceEquals(listeners[i], listener))
                {
                    listeners.RemoveAt(i);
                    break;
                }
            }
        }
    }

    // EXISTING (modified): Add a specific listener
    internal void AddListener(ActivityListener listener)
    {
        _listeners ??= new SynchronizedList<ActivityListener>();

        lock (_listeners)
        {
            _listeners.Add(listener);
        }
    }
}
```

#### Thread Safety Considerations

The key concurrency scenario is:

```text
Thread A: ActivitySource.StartActivity()     Thread B: listener.Resubscribe()
  │                                            │
  ├─ Read _listeners                           ├─ Lock s_activeSources
  ├─ Iterate listeners                         ├─ Evaluate ShouldListenTo
  ├─ Call listener.Sample(ref options)         ├─ source.RemoveListener(this)
  │                                            │
  └─ Activity created (or null)                └─ Continue iteration
```

This is safe because:

1. **`StartActivity` reads the listener list with a lock** (existing behavior in
   the runtime). If `RemoveListener` runs concurrently, it either:
   - Completes before `StartActivity` reads → listener not in list → not called
   - Completes after `StartActivity` reads → listener called one last time →
     safe

2. **In-flight activities are self-contained.** Once `StartActivity` returns a
   non-null `Activity`, that activity will call `ActivityStopped` on the
   listener regardless of subscription state. The activity holds a reference to
   the callbacks, not the subscription.

3. **Lock ordering is consistent.** `Resubscribe()` locks `s_activeSources`
   (same as `AddActivityListener`), then individual source locks. This matches
   the existing lock hierarchy.

#### Race Condition: New ActivitySource During Resubscribe

```text
Thread A: new ActivitySource("Foo")         Thread B: listener.Resubscribe()
  │                                            │
  ├─ Lock s_activeSources                      │ (waiting for lock)
  ├─ Add to s_activeSources                    │
  ├─ Evaluate listeners' ShouldListenTo        │
  ├─ Subscribe matching listeners              │
  └─ Release lock                              ├─ Lock s_activeSources
                                               ├─ Iterate (includes "Foo")
                                               ├─ Evaluate ShouldListenTo
                                               └─ Already subscribed → no-op
```

This is safe — the new source either:

- Is created before `Resubscribe` acquires the lock → included in iteration
- Is created after `Resubscribe` releases the lock → the source constructor
  already evaluates `ShouldListenTo` for all listeners (existing behavior)

---

### Proposal B: Expose `ActivityCreationOptions<T>.Source`

#### API Surface — Proposal B

```csharp
namespace System.Diagnostics
{
    public readonly struct ActivityCreationOptions<T>
    {
        // Existing members...
        public T Parent { get; }
        public ActivityTagsCollection SamplingTags { get; }
        public ActivityTraceId TraceId { get; }
        public string Name { get; }
        public ActivityKind Kind { get; }
        public IEnumerable<KeyValuePair<string, object?>>? Tags { get; }
        public IEnumerable<ActivityLink>? Links { get; }
        public string? TraceState { get; set; }

        /// <summary>
        /// Gets the <see cref="ActivitySource"/> that is requesting
        /// the creation of this activity.
        /// </summary>
        public ActivitySource Source { get; }
    }
}
```

#### Why This Is a Minimal Change

The `ActivitySource` reference **already exists** inside
`ActivityCreationOptions<T>`. The constructor receives it:

```csharp
// Current runtime code (simplified from ActivitySource.cs):
public Activity? StartActivity(
    string name,
    ActivityKind kind = ActivityKind.Internal,
    ActivityContext parentContext = default,
    /* ... */)
{
    // The ActivitySource passes 'this' to the constructor
    var options = new ActivityCreationOptions<ActivityContext>(
        this,          // ← ActivitySource is already here
        name,
        parentContext,
        kind,
        tags,
        links,
        idFormat);

    // Call listener.Sample(ref options)
    // ...
}
```

The `ActivitySource` is stored in a private field and used internally. Exposing
it as a public property is a one-line change:

```csharp
// In ActivityCreationOptions<T>
private readonly ActivitySource _source;

// NEW: public getter for existing private field
public ActivitySource Source => _source;
```

#### Impact on OpenTelemetry SDK

With `Source` exposed, the OTel SDK could use a single listener with a dynamic
`Sample` gate:

```csharp
// In TracerProviderSdk constructor
internal class TracerProviderSdk
{
    private volatile ImmutableHashSet<string> _enabledSources;

    internal TracerProviderSdk(/* ... */)
    {
        _enabledSources = state.Sources
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

        var activityListener = new ActivityListener();

        // Subscribe to everything (or use a broad pattern)
        activityListener.ShouldListenTo = _ => true;

        activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
        {
            if (Sdk.SuppressInstrumentation)
                return ActivitySamplingResult.None;

            // Dynamic source gate — uses the newly exposed property
            if (!_enabledSources.Contains(options.Source.Name))
                return ActivitySamplingResult.None;

            // Normal sampling path
            return ComputeActivitySamplingResult(ref options, this.Sampler);
        };

        // ... rest of setup
    }

    /// <summary>
    /// Dynamically enables activity collection from the named source.
    /// Takes effect immediately on the next StartActivity call.
    /// </summary>
    public void EnableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Add(name),
            sourceName);
    }

    /// <summary>
    /// Dynamically disables activity collection from the named source.
    /// In-flight activities complete normally. No new activities are created.
    /// </summary>
    public void DisableSource(string sourceName)
    {
        ImmutableInterlocked.Update(
            ref _enabledSources,
            static (set, name) => set.Remove(name),
            sourceName);
    }
}
```

#### Performance Analysis

```text
StartActivity for ENABLED source (normal path):
  1. ShouldListenTo: _ => true                     ~1ns (always true)
  2. Sample delegate entry                         ~2ns
  3. Sdk.SuppressInstrumentation check             ~5ns (AsyncLocal read)
  4. ImmutableHashSet.Contains(options.Source.Name) ~20ns
  5. ComputeActivitySamplingResult                  ~50-200ns (sampler dependent)
  Total: ~78-228ns

StartActivity for DISABLED source:
  1. ShouldListenTo: _ => true                     ~1ns
  2. Sample delegate entry                         ~2ns
  3. Sdk.SuppressInstrumentation check             ~5ns
  4. ImmutableHashSet.Contains → false             ~20ns
  5. Return None                                    ~0ns
  Total: ~28ns — NO Activity allocated

StartActivity with NO listener subscribed (current behavior for unknown sources):
  Total: ~5ns (early exit in StartActivity)
```

The overhead for a disabled source is ~28ns vs ~5ns for a truly unsubscribed
source. For most applications, this ~23ns difference per `StartActivity` call is
negligible.

**Caveat:** `ShouldListenTo = _ => true` means the listener is subscribed to
*every* `ActivitySource` in the process. This includes sources from libraries
you don't care about. Each of those sources' `StartActivity` calls will invoke
the `Sample` delegate. In a process with many active sources, this adds up.

**Mitigation:** Use a broad-but-bounded `ShouldListenTo` instead:

```csharp
activityListener.ShouldListenTo = source =>
    _potentialSources.Contains(source.Name);
```

Where `_potentialSources` is a superset of all sources that *might* be enabled.
This limits the subscription scope while still allowing dynamic control within
that scope.

---

### Proposal C: `ActivitySource.RefreshListeners()` (Static)

An alternative to per-listener `Resubscribe()` that re-evaluates all
listener-source pairs globally:

```csharp
namespace System.Diagnostics
{
    public sealed class ActivitySource : IDisposable
    {
        /// <summary>
        /// Re-evaluates all <see cref="ActivityListener.ShouldListenTo"/>
        /// predicates against all <see cref="ActivitySource"/> instances,
        /// updating subscription state to reflect current predicate results.
        /// </summary>
        /// <remarks>
        /// This is a global operation that acquires internal locks.
        /// It should be called sparingly (e.g., in response to configuration
        /// changes), not on a hot path.
        /// </remarks>
        public static void RefreshListeners();
    }
}
```

This is simpler to implement (one method, no per-listener state) but coarser
grained — it refreshes everything, not just one listener.

---

### Recommendation: Combined Proposal A + B

We recommend implementing both proposals:

| Proposal | Enables | Cost |
| ---------- | --------- | ------ |
| **A: `Resubscribe()`** | True dynamic subscription/unsubscription with a single listener. Clean semantics. | Moderate runtime change (new internal methods on ActivitySource) |
| **B: Expose `Source`** | Dynamic filtering in `Sample` with zero allocation for disabled sources. Single listener, no re-subscription needed. | Trivial runtime change (one public property on existing private field) |

**Proposal B alone** covers 90% of use cases with minimal runtime change. The
only limitation is that `ShouldListenTo` must be broad enough to cover all
potential sources, which means the `Sample` delegate is called for sources that
will never be enabled. For most applications, this overhead is negligible.

**Proposal A** is needed when:

- You want zero overhead for disabled sources (not even a `Sample` call)
- You cannot predict the universe of potential sources
- You need true unsubscription for correctness (e.g., testing frameworks)

Together, they give the OTel SDK (and any `ActivityListener` consumer) full
dynamic control over activity subscription with minimal overhead.

---

## Appendix: Integration with OpenTelemetry SDK Architecture

### Where Dynamic Source Control Fits

```text
┌─────────────────────────────────────────────────────────────┐
│                  TracerProviderBuilder                       │
│                                                             │
│  AddSource("HttpClient")     ← static, build-time          │
│  AddSource("SqlClient")      ← static, build-time          │
│  AddPotentialSource("Redis") ← NEW: build-time declaration  │
│                                 of dynamically-controllable  │
│                                 sources                      │
│                                                             │
│  SetSampler(...)                                            │
│  AddProcessor(...)                                          │
│  AddExporter(...)                                           │
└──────────────────────────┬──────────────────────────────────┘
                           │ .Build()
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    TracerProviderSdk                         │
│                                                             │
│  EnableSource("Redis")    ← NEW: runtime control            │
│  DisableSource("Redis")   ← NEW: runtime control            │
│  GetEnabledSources()      ← NEW: introspection              │
│                                                             │
│  [existing]                                                 │
│  AddProcessor(...)        ← already supports post-build     │
│  Shutdown()                                                 │
│  Dispose()                                                  │
└─────────────────────────────────────────────────────────────┘
```

### Proposed SDK API

```csharp
// Builder extension
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Declares a source that can be dynamically enabled/disabled after
    /// the provider is built. The source is NOT enabled by default.
    /// </summary>
    public static TracerProviderBuilder AddDynamicSource(
        this TracerProviderBuilder builder, string name);

    /// <summary>
    /// Declares a source that can be dynamically enabled/disabled after
    /// the provider is built. The source IS enabled by default.
    /// </summary>
    public static TracerProviderBuilder AddDynamicSource(
        this TracerProviderBuilder builder, string name, bool enabledByDefault);
}

// Provider extension
public static class TracerProviderExtensions
{
    /// <summary>
    /// Enables activity collection from a source previously registered
    /// with <see cref="AddDynamicSource"/>.
    /// </summary>
    /// <returns>true if the source was found and enabled; false if the
    /// source was not registered as dynamic.</returns>
    public static bool EnableSource(this TracerProvider provider, string name);

    /// <summary>
    /// Disables activity collection from a source previously registered
    /// with <see cref="AddDynamicSource"/>.
    /// </summary>
    public static bool DisableSource(this TracerProvider provider, string name);

    /// <summary>
    /// Returns the set of currently enabled dynamic sources.
    /// </summary>
    public static IReadOnlyCollection<string> GetDynamicSources(
        this TracerProvider provider);
}
```

### SamplingParameters Enhancement

To propagate source identity through the OTel `Sampler` abstraction (independent
of the runtime proposals), `SamplingParameters` could be extended:

```csharp
public readonly struct SamplingParameters
{
    // Existing
    public ActivityContext ParentContext { get; }
    public ActivityTraceId TraceId { get; }
    public string Name { get; }
    public ActivityKind Kind { get; }
    public IEnumerable<KeyValuePair<string, object?>>? Tags { get; }
    public IEnumerable<ActivityLink>? Links { get; }

    // NEW: Source identity for source-aware sampling decisions
    public string? SourceName { get; }
    public string? SourceVersion { get; }
}
```

This would enable custom samplers to make source-aware decisions:

```csharp
public class SourceAwareSampler : Sampler
{
    private readonly Dictionary<string, Sampler> _perSourceSamplers;

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
    {
        if (parameters.SourceName is not null
            && _perSourceSamplers.TryGetValue(parameters.SourceName, out var sampler))
        {
            return sampler.ShouldSample(parameters);
        }

        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
```
