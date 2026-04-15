# Configuration Analysis - Risk Register

Risks and considerations identified during the [configuration
analysis](configuration-analysis.md), organised by the phase in which they must
be addressed. Each entry preserves the full analysis from the original audit.

**Date:** 2026-04-13 **Author:** Steve Gordon (with AI-assisted research)
**Driver:**
[open-telemetry/opentelemetry-dotnet#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)

---

## 1. Must Address in Step 2 (Config Provider Registration)

### 1.1 Options Validation Is Completely Absent

The codebase has **zero** `IValidateOptions<T>` implementations, **zero**
`[Range]`/`[Required]` DataAnnotation attributes, **zero** `ValidateOnStart`
calls, and **zero** `PostConfigure<T>` registrations. The validation pipeline in
`DelegatingOptionsFactory` (step 4 of the `Create` flow in [Deep Dive
B](configuration-analysis-deep-dives.md#b-delegatingoptionsfactory-simplification))
exists but is never exercised.

Today this is tolerable - config comes from code-controlled env vars and
`Configure<T>()` lambdas. Once YAML declarative config flows in, arbitrary
user-supplied strings populate options classes. Examples of invalid values that
would silently produce broken behaviour rather than failing fast at startup:

| Options Class | Property | Invalid Value | Consequence |
| --- | --- | --- | --- |
| `SamplerOptions` ([S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration)) | `SamplerArg` | `-5.0` or `999.0` (outside `[0.0, 1.0]`) | `TraceIdRatioBasedSampler` computes a nonsensical `idUpperBound`; sampling is effectively random |
| `BatchExportActivityProcessorOptions` | `MaxExportBatchSize` | `0` or negative | `BatchExportProcessor` never exports, or throws at construction |
| `OtlpExporterOptions` | `TimeoutMilliseconds` | `-1` | `HttpClient.Timeout` set to `Timeout.InfiniteTimeSpan` - requests never time out |
| `PeriodicExportingMetricReaderOptions` | `ExportIntervalMilliseconds` | `0` | Tight-loop export; CPU pegged |

Under runtime reload, the risk is amplified: an invalid value arriving via OpAMP
or a file change should be **rejected** (previous value retained) rather than
applied to a running system.

**Required pattern - validate before apply:**

```csharp
// IValidateOptions<SamplerOptions> implementation
public class SamplerOptionsValidator : IValidateOptions<SamplerOptions>
{
    public ValidateOptionsResult Validate(string? name, SamplerOptions options)
    {
        if (options.SamplerArg is < 0.0 or > 1.0)
            return ValidateOptionsResult.Fail(
                $"SamplerArg must be in [0.0, 1.0], got {options.SamplerArg}");
        return ValidateOptionsResult.Success;
    }
}
```

At startup, `ValidateOnStart` ensures the host fails fast before any telemetry
is collected. Under reload, validation runs inside the
`DelegatingOptionsFactory.Create` pipeline before the new options instance is
returned to `IOptionsMonitor` - invalid values cause
`OptionsValidationException`, which the `OnChange` callback must catch (see
[S2.3](#23-onchange-callback-exception-safety)).

**Recommendation:** Add `IValidateOptions<T>` implementations for every options
class that accepts external input. Register `ValidateOnStart<T>` for all options
types used in the startup path. This is a prerequisite for safe declarative
config and runtime reload.

### 1.2 Silent Configuration Failure Model vs Fail-Fast

All configuration parsing in the SDK uses `TryGet*` methods that **silently fall
back to defaults** when values are invalid:

```csharp
// BatchExportActivityProcessorOptions constructor
if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log,
    "OTEL_BSP_MAX_QUEUE_SIZE", out var maxQueueSize))
{
    this.MaxQueueSize = maxQueueSize;
}
// If parsing fails: default retained, EventSource warning logged (if listener attached)
```

The `IConfigurationExtensionsLogger.LogInvalidConfigurationValue(key, value)`
call generates an EventSource event, but EventSource logging is opt-in - unless
the user has attached a listener or enabled the `OTEL_DIAGNOSTICS.json`
self-diagnostics file, the error is completely invisible.

For env vars, this is a defensible choice - the SDK should not crash because an
inherited env var has an unexpected format. For declarative config, it is
dangerous: a YAML typo (`schedule_delay: "5s"` instead of `schedule_delay:
5000`) silently produces default behaviour. The user believes their config is
active; it isn't.

**Recommendation:** Introduce two failure modes based on the config source:

| Source | Failure mode | Rationale |
| --- | --- | --- |
| Environment variables | Silent fallback + EventSource warning (current behaviour) | Env vars may be inherited from parent processes; crashing is disproportionate |
| Declarative config file (`OTEL_CONFIG_FILE`) | Fail-fast at startup with actionable error message | The user explicitly authored this file; silent fallback masks bugs |
| Policy provider (OpAMP / HTTP) | Reject invalid value + EventSource warning; retain previous | The running system must not crash due to a bad remote push |

The fail-fast for declarative config can be implemented via `ValidateOnStart`
([S1.1](#11-options-validation-is-completely-absent)) combined with stricter
parsing in the YAML `IConfigurationProvider` that rejects structurally invalid
nodes before they enter the `IConfiguration` tree.

### 1.3 TryAddSingleton First-Wins - Silent Misconfiguration Risk

All `RegisterOptionsFactory<T>` calls use `TryAddSingleton<IOptionsFactory<T>>`
([S2.1](configuration-analysis.md#21-configuration-infrastructure)). First
registration wins; subsequent ones silently no-op.

If a consumer registers their own `IOptionsFactory<OtlpExporterOptions>`
(intentionally or via another library) **before** `AddOtlpExporter()`, the SDK's
`DelegatingOptionsFactory` registration is silently skipped. The consumer's
factory lacks the env-var-first priority model, the `IConfiguration`-aware
constructor, and the named-options wiring. Options appear to work but produce
wrong values - env vars are ignored, configure delegates run in an unexpected
order.

This risk grows with the component registry design ([Deep Dive
E](configuration-analysis-deep-dives.md#e-component-registry-detailed-design)).
Vendor packages calling `AddMyVendorExporterComponents()` could register
factories for shared options types in unpredictable order depending on how the
user orders their startup code.

**Recommendation:** `RegisterOptionsFactory<T>` should log a diagnostic event
via EventSource when it detects a pre-existing `IOptionsFactory<T>` registration
and skips its own. This gives users visibility into the silent no-op. The
message should identify the existing registration type so the user can determine
whether the conflict is intentional.

```csharp
internal static IServiceCollection RegisterOptionsFactory<T>(...)
{
    bool added = services.TryAddSingleton<IOptionsFactory<T>>(...);
    if (!added)
    {
        OpenTelemetrySdkEventSource.Log.OptionsFactoryRegistrationSkipped(
            typeof(T).Name,
            existingDescriptor.ImplementationType?.Name ?? "unknown");
    }
    return services;
}
```

Note: `TryAddSingleton` does not return a bool today - the detection would
require a `services.Any(d => d.ServiceType == typeof(IOptionsFactory<T>))` check
before the `TryAdd` call.

### 1.4 Public Parameterless Constructors Bypass Declarative Config

Every options class in the SDK has **two** constructors:

```csharp
// Public - creates its own IConfiguration from env vars only
public OtlpExporterOptions()
    : this(new ConfigurationBuilder().AddEnvironmentVariables().Build()) { }

// Internal - receives the host's IConfiguration (used by DelegatingOptionsFactory)
internal OtlpExporterOptions(IConfiguration configuration) { ... }
```

This pattern is consistent across `OtlpExporterOptions`,
`BatchExportActivityProcessorOptions`, `BatchExportLogRecordProcessorOptions`,
`PeriodicExportingMetricReaderOptions`, `SdkLimitOptions`,
`ExperimentalOptions`, and `ZipkinExporterOptions`.

The public constructor creates an isolated `IConfiguration` backed only by
`AddEnvironmentVariables()`. It does **not** see:

- Declarative config YAML values
- `appsettings.json` / user secrets
- `TelemetryPolicyConfigurationProvider` policy updates
- Any custom `IConfigurationProvider` registered with the host

This means:

1. **Users constructing options directly** (`new OtlpExporterOptions()` for
   manual exporter wiring) will not pick up declarative config values. This is a
   non-obvious behavioural split.

2. **Test code** that creates options instances directly will not see
   declarative config, which may mask integration issues.

3. **The existing public constructor for `OtlpTraceExporter`**
   (`OtlpTraceExporter(OtlpExporterOptions)`) creates a no-reload snapshot path
   (already noted in [Deep Dive
   C.3.1](configuration-analysis-deep-dives.md#c3-solution-approaches)). Users
   who construct via this path also miss declarative config unless they
   explicitly build the options from the host's `IConfiguration`.

**Recommendation:** Document this as a known limitation with clear guidance: "To
receive values from declarative config, appsettings.json, or policy providers,
resolve options from DI via `IOptionsMonitor<T>.Get(name)` or
`IOptions<T>.Value`. Direct construction via `new OtlpExporterOptions()` reads
environment variables only."

For the proposed new options classes (`SamplerOptions`, `ResourceOptions`,
`PropagatorOptions`), consider whether the public parameterless constructor
should even exist. If the class is only meaningful within the DI/options
pipeline, an `internal`-only constructor receiving `IConfiguration` is cleaner.
However, this breaks the convention established by existing options classes and
prevents direct use in tests without DI.

### 1.5 PostConfigure Gap for Fallback Chains Under Reload

`SdkLimitOptions` ([Deep Dive
A.13](configuration-analysis-deep-dives.md#a13-sdklimitoptions)) implements
cascading fallback logic: `SpanEventAttributeCountLimit` ->
`SpanAttributeCountLimit` -> `AttributeCountLimit`. This logic runs in the
**constructor**, using `bool xxxSet` flags to track explicit vs inherited
values. The `DelegatingOptionsFactory` pipeline is: factory delegate
(constructor) -> `Configure<T>` delegates -> `PostConfigure<T>` -> Validate.

Under reload, a user's `Configure<SdkLimitOptions>` delegate might set
`SpanAttributeCountLimit = 50` but not set `SpanEventAttributeCountLimit`. The
fallback should cascade - `SpanEventAttributeCountLimit` should inherit the new
value of `50`. But the constructor already ran before the `Configure` delegate --
the cascade was evaluated with the old/default values. The `Configure` delegate
sets the property, but the cascade relationship is not re-evaluated because the
constructor logic has already executed.

This is exactly what `PostConfigure<T>` is designed for: running after all
`Configure` delegates to apply derived/cascading logic.

**Affected options classes:**

| Options Class | Cascading Properties | Current Location of Cascade Logic |
| --- | --- | --- |
| `SdkLimitOptions` | 8 signal-specific properties cascade to 2 generic properties | Constructor (`bool xxxSet` flags) |
| `OtlpExporterOptions` | `AppendSignalPathToEndpoint` depends on whether `Endpoint` was explicitly set | Constructor |
| `ActivityExportProcessorOptions` | `BatchExportProcessorOptions` resolved by name from inner options | Factory delegate |
| `MetricReaderOptions` | `PeriodicExportingMetricReaderOptions` resolved by name from inner options | Factory delegate |

**Required change for `SdkLimitOptions`:** Move the cascade/fallback logic from
the constructor to a `PostConfigure<SdkLimitOptions>` registration. The
constructor reads `IConfiguration` keys and sets only explicitly-configured
values. `PostConfigure` then applies the cascade chain after all `Configure`
delegates have run:

```csharp
services.PostConfigure<SdkLimitOptions>((options) =>
{
    // Apply fallback chain: signal-specific -> generic -> default
    options.SpanAttributeCountLimit ??= options.AttributeCountLimit ?? 128;
    options.SpanEventAttributeCountLimit ??= options.SpanAttributeCountLimit;
    options.SpanLinkAttributeCountLimit ??= options.SpanAttributeCountLimit;
    // ... remaining cascades
});
```

This ensures the cascade correctly reflects any value set by a `Configure`
delegate, including values from declarative config or runtime policy changes.

### 1.6 Configuration Key Translation - YAML snake_case to IConfiguration Keys

The OTel spec uses different naming conventions across config surfaces:

| Surface | Convention | Example |
| --- | --- | --- |
| Environment variables | `UPPER_SNAKE_CASE` | `OTEL_TRACES_SAMPLER` |
| Declarative YAML keys | `snake_case` | `traces.sampler.type` |
| .NET `IConfiguration` keys | `:` hierarchy, case-insensitive | `OTEL_TRACES_SAMPLER` or `sdk:traces:sampler:type` |
| .NET `appsettings.json` | `PascalCase` by convention | `"OpenTelemetry:Sampler:SamplerType"` |

The YAML `IConfigurationProvider` must project YAML's hierarchical `snake_case`
keys into the `IConfiguration` key space. Two strategies exist:

**Strategy A - Project YAML keys as env var equivalents:**

Map `sdk.traces.sampler` to `OTEL_TRACES_SAMPLER` so that existing options
constructors (which read `configuration["OTEL_TRACES_SAMPLER"]`) pick up YAML
values without modification.

```text
YAML: sdk.traces.sampler.type = "traceidratio"
  -> IConfiguration key: "OTEL_TRACES_SAMPLER" = "traceidratio"

YAML: sdk.traces.processors[0].batch.schedule_delay = 5000
  -> IConfiguration key: "OTEL_BSP_SCHEDULE_DELAY" = "5000"
```

**Pros:** Zero changes to existing options constructors. Env var and YAML config
flow through the same keys. **Cons:** The YAML provider must maintain a mapping
table from spec YAML paths to env var names. Adding a new YAML key requires
updating the mapping.

**Strategy B - Project YAML keys as hierarchical `IConfiguration` sections:**

Map `sdk.traces.sampler.type` to `sdk:traces:sampler:type`. Options classes gain
a second reading path that checks both their env var key and the hierarchical
section key.

```text
YAML: sdk.traces.sampler.type = "traceidratio"
  -> IConfiguration key: "sdk:traces:sampler:type" = "traceidratio"
```

**Pros:** Natural `IConfiguration` hierarchy; `GetSection("sdk:traces:sampler")`
works for subtree binding. **Cons:** Every options constructor needs to read
from two key formats. Named options binding via subtrees ([Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories))
is more natural with this approach.

**Recommendation:** Strategy A for env-var-equivalent keys (limits, batch
settings, sampler), Strategy B for component tree structure (processors,
exporters, propagator lists). The YAML provider maps leaf values to env var keys
where a 1:1 mapping exists, and projects structural arrays as hierarchical
`IConfiguration` sections. This hybrid approach requires a well-defined mapping
table but avoids modifying existing options constructors for the common case.

### 1.7 The SDK's Internal EnvironmentVariablesConfigurationProvider Copy

The SDK maintains a manually vendored copy of the .NET runtime's
`EnvironmentVariablesConfigurationProvider`,
`EnvironmentVariablesConfigurationSource`, and `EnvironmentVariablesExtensions`
in `src/Shared/EnvironmentVariables/`. The three files are marked `//
<auto-generated />` (to suppress StyleCop) and compiled into each consuming
project via `<Compile Include>` link items. They were vendored in [PR

 4092](<https://github.com/open-telemetry/opentelemetry-dotnet/pull/4092>)

(January 2023) to remove the direct package dependency on
`Microsoft.Extensions.Configuration.EnvironmentVariables`.

This is the provider used by the fallback `IConfiguration` in the public
parameterless constructors
([S1.4](#14-public-parameterless-constructors-bypass-declarative-config)). Seven
call sites across the SDK use the pattern `new
ConfigurationBuilder().AddEnvironmentVariables().Build()`:

- `OtlpExporterOptions` - parameterless constructor
- `ExperimentalOptions` - parameterless constructor
- `SdkLimitOptions` - parameterless constructor
- `ZipkinExporterOptions` - parameterless constructor
- `BatchExportActivityProcessorOptions` - parameterless constructor
- `BatchExportLogRecordProcessorOptions` - parameterless constructor
- `PeriodicExportingMetricReaderOptions` - parameterless constructor
- `ResourceBuilderExtensions` - lazy fallback `IConfiguration`
- `ProviderBuilderServiceCollectionExtensions` - DI fallback registration

**There is no automated sync mechanism.** Updates from the runtime must be
identified and applied manually. The vendored copy has not been updated since it
was introduced.

**Maintenance and divergence risk:** If the runtime's
`EnvironmentVariablesConfigurationProvider` gains bug fixes, security patches,
or behavioural changes (e.g., refinements to the `__` -> `:` key normalisation
logic), the SDK's copy diverges silently. Since the copy carries no version
indicator, there is no way to determine at a glance which runtime version it
corresponds to or what fixes may be missing.

**Confirmed: `Microsoft.Extensions.Configuration.EnvironmentVariables` is NOT a
transitive dependency.** The SDK's dependency chain via
`Microsoft.Extensions.Logging.Configuration` pulls in
`Microsoft.Extensions.Configuration` (core),
`Microsoft.Extensions.Configuration.Abstractions`,
`Microsoft.Extensions.Configuration.Binder`, and
`Microsoft.Extensions.Options.ConfigurationExtensions` - but **not** the
EnvironmentVariables package. The vendored copy is therefore currently necessary
for the code to compile.

**Recommendation: Replace the vendored copy with a direct package dependency.**
The `Microsoft.Extensions.Configuration.EnvironmentVariables` package is
lightweight and introduces no new transitive dependencies beyond what the SDK
already resolves. The change would:

1. **Eliminate the divergence risk** - the SDK automatically picks up bug fixes
   and security patches from the runtime's implementation.
2. **Reduce maintenance burden** - three fewer vendored files to track.
3. **Align with the existing pattern** - the SDK already takes direct
   dependencies on `Microsoft.Extensions.Diagnostics.Abstractions` and
   `Microsoft.Extensions.Logging.Configuration`; one more M.E.Configuration
   package is consistent.

The package version should follow the same per-TFM version strategy already used
in `Directory.Packages.props` (8.0.0 for net8.0, 9.0.0 for net9.0, 10.0.0 for
net10.0). After adding the dependency, remove the three files from
`src/Shared/EnvironmentVariables/`, remove the `<Compile Include>` link items
from consuming `.csproj` files, and verify the public
`AddEnvironmentVariables()` extension method from the package resolves correctly
at all call sites (the vendored copy uses `internal` visibility, so any
unintended public API surface change should be reviewed).

---

## 2. Must Address Before Reload Implementation

### 2.1 Non-Atomic Multi-Options Reload (Torn Reads Across Options Types)

The OTLP exporter consumes `OtlpExporterOptions`, `SdkLimitOptions`, and
`ExperimentalOptions` simultaneously. The reload design ([Deep Dive
C](configuration-analysis-deep-dives.md#c-otlp-exporter-snapshot-architecture-and-reload-path),
[S4.4](configuration-analysis.md#44-telemetry-policies-architecture)) wires
independent `IOptionsMonitor<T>.OnChange` subscriptions for each type. But
`IOptionsMonitor` provides **no transactional guarantee** across options types.

When `TelemetryPolicyConfigurationProvider.UpdatePolicies()` fires `OnReload()`,
each `IOptionsMonitor<T>` recomputes independently. There is a window where a
component sees new `SdkLimitOptions` (e.g., tighter attribute limits) but old
`OtlpExporterOptions` (old endpoint), or vice versa. The `OnChange` callbacks
for different `T` types fire in indeterminate order.

**Affected scenarios:**

| Component | Options consumed together | Risk of torn state |
| --- | --- | --- |
| OTLP exporter | `OtlpExporterOptions` + `SdkLimitOptions` + `ExperimentalOptions` | New limits applied with old export client, or vice versa |
| `TracerProviderSdk` | `SamplerOptions` + future `ResourceOptions` | New sampler with old resource attributes |
| Declarative config | All options bound from a single YAML file | YAML "transaction" split across multiple `IOptionsMonitor<T>` recomputes |

**Consistency model:** The design accepts **eventual consistency** - after all
`OnChange` callbacks have fired, the system is fully consistent. During the
transition window (typically sub-millisecond for in-process changes), a small
number of operations may see mixed old/new state.

This is acceptable for the identified reload scenarios because each options
change is independently safe:

- Changing SDK limits doesn't break in-flight exports to the old endpoint
- Changing sampler rate doesn't interact with resource attributes
- The OTLP exporter's export-client swap ([Deep Dive
  C.3](configuration-analysis-deep-dives.md#c3-solution-approaches)) is
  self-contained

**When this matters:** If a future scenario requires coupled options changes
(e.g., `Protocol` change paired with `Endpoint`), a composite options class or a
sequence-number/version check would be needed. The current design should
document that Protocol is Tier 3 restart-required (already stated in [Deep Dive
C.2](configuration-analysis-deep-dives.md#c2-the-four-reload-barriers)) partly
for this reason.

### 2.2 OnChange Subscription Lifecycle and Disposal

The reload design proposes `IOptionsMonitor<T>.OnChange` subscriptions in
`TracerProviderSdk` ([Deep Dive
D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper)),
`BatchExportProcessor`
([S4.5](configuration-analysis.md#45-recommended-build-order) Step 4), and
`OtlpTraceExporter` ([Deep Dive
C.3.1](configuration-analysis-deep-dives.md#c3-solution-approaches)). Each
`OnChange` call returns an `IDisposable` registration token. Currently, **no
provider, processor, or exporter `Dispose` method** handles options subscription
cleanup - because no subscriptions exist today.

If a provider is disposed but `IOptionsMonitor<T>` outlives it (common when the
DI container hasn't been disposed yet - e.g., during graceful shutdown where the
provider shuts down before the host's `IServiceProvider` is disposed), the
`OnChange` callback fires against a disposed component:

- `ReloadableSampler.UpdateSampler()` writes to a sampler that
  `TracerProviderSdk` has already shut down
- `ReloadableOtlpExportClient.Swap()` attempts to create a new `HttpClient` on a
  disposed exporter
- `BatchExportProcessor` export-enable toggle fires after the processor's worker
  thread has exited

**Required pattern:** Every component that subscribes to `OnChange` must:

1. Store the `IDisposable` token returned by `OnChange`
2. Call `token.Dispose()` in its own `Dispose()` / `Shutdown()` method,
   **before** disposing internal resources
3. Use a disposed/shutdown flag check as a belt-and-suspenders guard inside the
   callback itself

```csharp
// Required pattern for all reload-capable components
private readonly IDisposable? optionsChangeSubscription;
private volatile bool disposed;

// In constructor:
this.optionsChangeSubscription = optionsMonitor.OnChange((opts, name) =>
{
    if (this.disposed) return;  // guard against post-dispose callback
    // ... apply change
});

// In Dispose/Shutdown:
this.disposed = true;
this.optionsChangeSubscription?.Dispose();
// ... then dispose internal resources
```

**Affected designs:** [Deep Dive
C.3](configuration-analysis-deep-dives.md#c3-solution-approaches) (shows
`changeListener` field but doesn't trace to disposal), [Deep Dive
D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper)
(no disposal shown),
[S4.4](configuration-analysis.md#44-telemetry-policies-architecture) (end-to-end
flow doesn't mention subscription lifecycle).

### 2.3 OnChange Callback Exception Safety

`IOptionsMonitor<T>.OnChange` callbacks run synchronously in the options
infrastructure. If a callback throws (e.g., `CreateSamplerFromOptions` receives
an unrecognised sampler type string, or `HttpClientFactory.Invoke()` fails
during OTLP client recreation), the exception propagates through the options
change notification pipeline. In `Microsoft.Extensions.Options`, this can
prevent subsequent `OnChange` listeners **for the same options type** from
firing - an exception in the sampler reload callback could block the SDK-limits
reload callback if both are registered against the same underlying
`IChangeToken`.

The proposed designs in [Deep Dive
C.3](configuration-analysis-deep-dives.md#c3-solution-approaches), [Deep Dive
D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper),
and [S4.4](configuration-analysis.md#44-telemetry-policies-architecture) show
`OnChange` callbacks without exception handling.

**Required pattern - catch, log, retain previous value:**

```csharp
optionsMonitor.OnChange((opts, name) =>
{
    if (this.disposed || name != this.optionsName) return;
    try
    {
        var newSampler = CreateSamplerFromOptions(opts);
        reloadableSampler.UpdateSampler(newSampler);
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
            $"Sampler updated to '{newSampler.GetType().Name}' from options change.");
    }
    catch (Exception ex)
    {
        // Log and retain previous sampler - never propagate
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
            $"Sampler reload failed, retaining current sampler: {ex.Message}");
    }
});
```

This pattern integrates with [S1.1](#11-options-validation-is-completely-absent)
(validation): `IValidateOptions<T>` catches structural invalidity during
`DelegatingOptionsFactory.Create()` before the callback fires, while the
try/catch in the callback handles operational failures (network errors, resource
exhaustion, etc.) that validation cannot predict.

### 2.4 Hot-Path Performance - readonly Fields vs Live Options Reads

Every hot-path configuration value in the SDK is currently a `readonly` field or
get-only property cached at construction. No hot-path code reads from
`IOptionsMonitor`:

| Component | Hot-path field(s) | Access frequency | Current storage |
| --- | --- | --- | --- |
| `TracerProviderSdk.Sampler` | Sampler instance | Per-span | `internal Sampler Sampler { get; }` (get-only) |
| `BatchExportProcessor` | `MaxExportBatchSize`, `ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds` | Per-item / per-batch cycle | `internal readonly int` |
| `OtlpTraceExporter` | `sdkLimitOptions`, `startWritePosition`, `transmissionHandler` | Per-export batch | `private readonly` |
| `OpenTelemetryLogger` | `options` (bool flags) | Per-log-record | `private readonly OpenTelemetryLoggerOptions` |
| `PeriodicExportingMetricReader` | `ExportIntervalMilliseconds`, `ExportTimeoutMilliseconds` | Per-export cycle | `internal readonly int` |

The reload design must **not** replace `readonly` field reads with per-operation
`IOptionsMonitor.Get(name)` calls. `IOptionsMonitor.Get(name)` does a
`ConcurrentDictionary` lookup on every invocation - acceptable for setup and
periodic operations, but not for per-span or per-log-record hot paths.

**Required pattern - event-driven cache update:**

The reload model proposed in [Deep Dive
D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper)
(`ReloadableSampler`) and [Deep Dive
C.3](configuration-analysis-deep-dives.md#c3-solution-approaches) (OTLP
exporter) already follows the correct pattern: `OnChange` fires on configuration
change (rare event), the callback swaps a `volatile` reference or field
(one-time cost), and all subsequent hot-path reads see the new value via the
`volatile` read (per-operation cost: one `volatile` load - equivalent to a
regular read on x86, a memory barrier on ARM).

This pattern must be applied consistently to all reload-capable components:

| Component | Reload mechanism | Hot-path read mechanism |
| --- | --- | --- |
| Sampler | `ReloadableSampler._inner` (`volatile` reference swap) | `_inner.ShouldSample()` - one virtual dispatch per span |
| OTLP exporter | `ReloadableOtlpExportClient` or `volatile` handler swap | `this.transmissionHandler` - `volatile` field read per export |
| SDK limits | `volatile SdkLimitOptions` reference in serializer | Direct property access per batch (already per-batch, not per-span) |
| Logger flags | `volatile OpenTelemetryLoggerOptions` reference | Direct property access per log record |
| Batch intervals | `volatile int` or timer `Change()` call | Timer-driven - no per-item read of the interval value |
| Metric export interval | Timer `Change()` call | Timer-driven - no per-metric read |

For `BatchExportProcessor`, the `readonly` modifier on `MaxExportBatchSize`,
`ScheduledDelayMilliseconds`, and `ExporterTimeoutMilliseconds` must be removed
and replaced with `volatile` - or more precisely, the worker's copy of these
values must be updateable. The worker thread loop in `BatchExportWorker` reads
`ScheduledDelayMilliseconds` as a `WaitHandle.WaitAny` timeout on each cycle, so
a `volatile` field naturally picks up the new value on the next cycle iteration
with no additional mechanism.

`MaxExportBatchSize` is more delicate: it is compared against
`circularBuffer.Count` on the **producer** hot path (per-item `TryExport`).
Making this `volatile` adds a per-item volatile read, which is acceptable on x86
(no fence) but adds a load-acquire on ARM. The alternative is to leave
`MaxExportBatchSize` as restart-required and only support interval reload - a
pragmatic choice given that batch size changes are rare.

### 2.5 Disposal Race During Component Swap - Drain Semantics

The [Deep Dive C.3](configuration-analysis-deep-dives.md#c3-solution-approaches)
code sketch shows:

```csharp
var old = Interlocked.Exchange(ref this.transmissionHandler, newHandler);
old.Dispose();   // drain in-flight requests first
```

The comment says "drain in-flight requests first" but `Dispose()` does not drain
-- it terminates. A thread that read `this.transmissionHandler` before the
`Interlocked.Exchange` holds a live reference to the old handler and may be
mid-`SendExportRequest`. Disposing the underlying `HttpClient` while a request
is in-flight causes `TaskCanceledException` or `ObjectDisposedException` in the
export path.

**Required drain protocol:**

```csharp
optionsMonitor.OnChange((newOpts, name) =>
{
    if (this.disposed || name != this.optionsName) return;
    if (!HasMeaningfulChange(this.currentSnapshot, newOpts)) return;

    try
    {
        var newClient = BuildExportClient(newOpts);
        var newHandler = BuildTransmissionHandler(newOpts, newClient);

        // Step 1: Atomic swap - new operations use new handler immediately
        var oldHandler = Interlocked.Exchange(ref this.transmissionHandler, newHandler);

        // Step 2: Drain - give in-flight operations time to complete on old handler
        // Shutdown returns true if all in-flight requests completed within the timeout
        oldHandler.Shutdown(drainTimeoutMilliseconds: 5000);

        // Step 3: Dispose - safe now; in-flight operations either completed or timed out
        oldHandler.Dispose();
        this.currentSnapshot = newOpts;
    }
    catch (Exception ex)
    {
        OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(ex.Message);
    }
});
```

The three-step sequence (swap -> drain -> dispose) ensures:

- New operations immediately use the new handler (step 1)
- In-flight operations on the old handler have a bounded window to complete
  (step 2)
- The old handler's `HttpClient` is only disposed after the drain window (step
  3)

For `ReloadableSampler`, the swap is inherently safe because
`Sampler.ShouldSample()` is a pure function with no resources to drain - the
`volatile` reference swap is sufficient. However, vendor-supplied custom
samplers (via `ISamplerFactory`) might hold resources. The `ISamplerFactory`
guidance should specify that `ShouldSample` must be stateless and thread-safe,
and that the SDK may replace the sampler instance at any time without calling
`Dispose` on the old one.

### 2.6 HttpClientFactory Delegate Preservation Under Reload

`OtlpExporterOptions.HttpClientFactory` is a `Func<HttpClient>` - a delegate
property set programmatically via a `Configure<OtlpExporterOptions>` callback.
It cannot be expressed in YAML or any `IConfiguration` source.

The default factory captures `this` (the options instance) and reads
`TimeoutMilliseconds` at invocation time. The
`TryEnableIHttpClientFactoryIntegration` method replaces it with a DI-backed
delegate for traces and metrics.

Under reload ([Deep Dive
C.3](configuration-analysis-deep-dives.md#c3-solution-approaches)), when
`IOptionsMonitor<OtlpExporterOptions>` fires `OnChange`, the
`DelegatingOptionsFactory.Create` pipeline runs:

1. Factory delegate creates a new `OtlpExporterOptions` from `IConfiguration`
   (new endpoint, headers, timeout)
2. `Configure<OtlpExporterOptions>` delegates run - **including the user's
   delegate that set HttpClientFactory**
3. PostConfigure runs
4. Validate runs
5. New options instance returned to `OnChange` callback

Step 2 is the critical one: if the user registered
`services.Configure<OtlpExporterOptions>(o => o.HttpClientFactory = myFactory)`,
it runs on every options creation - including reload. The custom factory is
preserved. This is correct.

**Risk case:** If the user set `HttpClientFactory` directly on a pre-constructed
options instance passed to `AddOtlpExporter(configure: o => o.HttpClientFactory
= ...)`, the delegate is registered via
`services.Configure<OtlpExporterOptions>(name, configure)`. Named `Configure`
delegates only run for the matching name. If the reload triggers a different
name (or if the unnamed-options path is in use), the delegate may not apply.

**Recommendation:** Document that `HttpClientFactory` set via named
`Configure<T>` delegates is preserved across reload for the same named options
instance. The unnamed-options path ([Deep Dive
C.2](configuration-analysis-deep-dives.md#c2-the-four-reload-barriers)) does not
participate in reload and is not affected. The `IHttpClientFactory` integration
path (which replaces `HttpClientFactory` internally) is safe because it reads
from DI on each invocation.

### 2.7 Scoped Options Lifetime When DisableOptionsReloading Is Removed

The [S2.1](configuration-analysis.md#21-configuration-infrastructure)
refactoring proposes removing
`DisableOptionsReloading<OpenTelemetryLoggerOptions>`. Currently,
`SingletonOptionsManager` replaces both `IOptionsMonitor<T>` (singleton) and
`IOptionsSnapshot<T>` (scoped). Removing it restores the default framework
registrations.

`IOptionsSnapshot<T>` creates a **new options instance per DI scope**. In
ASP.NET Core, every HTTP request is a scope. This means
`OpenTelemetryLoggerOptions` would be re-resolved per request - running the full
`DelegatingOptionsFactory` pipeline (factory delegate -> all `Configure<T>`
delegates -> all `PostConfigure<T>` -> all `IValidateOptions<T>`) on every
request. For pure-value bool flags this is cheap but non-zero.

More importantly, if any component resolves
`IOptionsSnapshot<OpenTelemetryLoggerOptions>` (which the logging infrastructure
might do internally), it gets a per-request instance that could momentarily
differ from what `IOptionsMonitor<T>.CurrentValue` returns during a reload
transition.

**Constraint:** The SDK's internal components must always consume
`IOptionsMonitor<T>.CurrentValue` (singleton-like, change-notified), never
`IOptionsSnapshot<T>`. This applies to all options types, not just
`OpenTelemetryLoggerOptions`.

For this refactoring specifically, the recommended approach is:

1. Remove `DisableOptionsReloading<OpenTelemetryLoggerOptions>` (which
   suppresses both `IOptionsMonitor` and `IOptionsSnapshot`)
2. Ensure `OpenTelemetryLogger` reads from
   `IOptionsMonitor<OpenTelemetryLoggerOptions>.CurrentValue` (which it already
   does in the current code)
3. Do **not** suppress `IOptionsSnapshot` separately - the framework default is
   fine because no SDK internal component should be resolving it. Advanced
   consumers who explicitly resolve `IOptionsSnapshot` are making a deliberate
   choice.

### 2.8 OnChange Callback Threading Model

`IOptionsMonitor<T>.OnChange` callbacks fire on the thread that triggered the
`IConfigurationProvider` reload. This has implications for the reload design:

| Source type | Callback thread | Constraint |
| --- | --- | --- |
| File watcher | ThreadPool thread (from `FileSystemWatcher.Changed`) | Must be thread-safe; must not block (blocks the file-watcher event loop) |
| OpAMP client | Network receive callback thread | Must be thread-safe; must not block (blocks subsequent message processing) |
| HTTP poller | ThreadPool thread (from `HttpClient` continuation) | Must be thread-safe |
| `IConfigurationRoot.Reload()` | Caller's thread | May be the application's main thread |

The `OnChange` callback holds an internal lock in the `OptionsMonitor<T>`
infrastructure during dispatch (`OptionsMonitor<T>._onChange` invocation). If
the callback performs a blocking operation (e.g., synchronous HTTP call to
create a new `HttpClient`, file I/O for TLS certificates), it blocks the
notification dispatch for all other `OnChange` subscribers of the same `T`.

**Constraint for the reload design:**

- `ReloadableSampler.UpdateSampler()` ([Deep Dive
  D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper)):
  Safe - a volatile reference swap is non-blocking.
- `SdkLimitOptions` reload: Safe - a volatile reference swap is non-blocking.
- `BatchExportProcessor` interval update: Safe - `Timer.Change()` is
  non-blocking.
- `OtlpExportClient` recreation ([Deep Dive
  C.3](configuration-analysis-deep-dives.md#c3-solution-approaches)):
  **Potentially blocking** - `HttpClientFactory.Invoke()` may do synchronous I/O
  (DNS resolution, TLS handshake if pre-connecting). The drain window
  ([S2.5](#25-disposal-race-during-component-swap---drain-semantics)) is also
  blocking.

For the OTLP exporter reload case, the `OnChange` callback should offload the
expensive client recreation to a `ThreadPool.QueueUserWorkItem` or equivalent,
performing only the decision logic (change detection, validation) synchronously:

```csharp
optionsMonitor.OnChange((newOpts, name) =>
{
    if (this.disposed || name != this.optionsName) return;
    if (!HasMeaningfulChange(this.currentSnapshot, newOpts)) return;
    // Offload expensive work to avoid blocking the options change dispatch
    ThreadPool.QueueUserWorkItem(_ =>
    {
        try { RebuildExportClient(newOpts); }
        catch (Exception ex) { LogChangeRejected(ex); }
    });
});
```

This introduces ordering complexity (two rapid changes could have their rebuilds
execute out of order), which would require a sequence number or CAS-based guard
in the rebuild method.

### 2.9 Resource Detector Declarative Config Gap

`OTEL_RESOURCE_DETECTORS` is a spec env var that is **not implemented**
([S2.4](configuration-analysis.md#24-spec-env-var-completeness)). Resources are
configured purely through the fluent `ResourceBuilder` API. Declarative config
specifies resources in YAML:

```yaml
sdk:
  resource:
    attributes:
      service.name: my-service
      deployment.environment: production
    detectors:
      enabled:
        - env
        - process
        - os
```

The `attributes` section maps naturally to `IConfiguration` key-value pairs. The
`detectors` section requires a registry pattern similar to the exporter/sampler
factories ([Deep Dive
E](configuration-analysis-deep-dives.md#e-component-registry-detailed-design)):

```csharp
public interface IResourceDetectorFactory
{
    string Name { get; }  // matches YAML key: "env", "process", "os"
    IResourceDetector Create(IConfiguration configuration, IServiceProvider services);
}
```

**Gap:** No `ResourceOptions` class exists (noted in
[S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration)).
Unlike sampler and exporter configuration, resource configuration has no
`IOptions<T>` integration at all - no env var constructor, no
`DelegatingOptionsFactory` registration, no named options support.

**Recommendation:** Add `ResourceOptions` following the same pattern as the
proposed `SamplerOptions`:

```csharp
public class ResourceOptions
{
    public string? ServiceName { get; set; }           // OTEL_SERVICE_NAME
    public string? ResourceAttributes { get; set; }    // OTEL_RESOURCE_ATTRIBUTES (comma-separated)
    public string[]? EnabledDetectors { get; set; }    // OTEL_RESOURCE_DETECTORS (future)
}
```

This unblocks declarative config for resources and provides the `IOptions<T>`
foundation for the factory registry to resolve detectors by name. Resource
reload is low-value (resources don't change at runtime), so
`IOptions<ResourceOptions>` (not `IOptionsMonitor`) is sufficient.

---

## 3. Must Address Before Telemetry Policies (Including OpAMP-Backed Sources)

### 3.1 OTEL_CONFIG_FILE vs IConfiguration Hierarchy - Resolution via SDK Option

The OTel spec states that when `OTEL_CONFIG_FILE` is set, environment variables
are **ignored** ([S2.4](configuration-analysis.md#24-spec-env-var-completeness)
Java comparison table). The analysis proposes the opposite: YAML config
participates as an `IConfigurationProvider` in the standard .NET hierarchy,
where env vars naturally layer on top. This is the right .NET-idiomatic choice,
but it contradicts the spec default behaviour.

**Resolution:** The spec provides an explicit [escape
hatch](https://github.com/open-telemetry/opentelemetry-specification/blame/dc3396967f1b1c68a452981a5c5d0344d723b109/specification/configuration/sdk-environment-variables.md#L338-L343):
"Implementations MAY provide a mechanism to customize the configuration model
parsed from `OTEL_CONFIG_FILE`."

Per [open-telemetry/opentelemetry-dotnet#6380
(comment)](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380#issuecomment-4237775220),
the pragmatic approach is an **SDK-specific configuration option** that controls
the behaviour:

- **Overlay mode (default for .NET):** The declarative config YAML file is
  registered as an `IConfigurationProvider` in the standard .NET pipeline.
  Environment variables, `appsettings.json`, user secrets, and other
  `IConfigurationProvider` sources layer on top according to their registration
  order. This maximises intuitiveness within the .NET ecosystem - declarative
  config provides defaults, programmatic `Configure<T>()` and env vars override.

- **Strict mode (spec-compliant):** The YAML file is the sole configuration
  source for OTel SDK settings. Environment variables for OTel-specific keys
  (those read by options constructors via
  `OpenTelemetryConfigurationExtensions`) are not read. This maximises
  consistency across polyglot environments where the same YAML file is deployed
  to Java, Go, and .NET services.

The Java SDK implements the equivalent mechanism via an [SPI
customizer](https://github.com/open-telemetry/opentelemetry-java/blob/main/sdk-extensions/incubator/src/main/java/io/opentelemetry/sdk/extension/incubator/fileconfig/DeclarativeConfigurationCustomizer.java#L24-L25)
that accepts the in-memory config model and returns a customised version.

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration(options =>
    {
        // Default: true - .NET IConfiguration sources layer on top of YAML
        // Set to false for strict spec-compliant behaviour (YAML only)
        options.AllowConfigurationOverlay = true;
    });
```

**Implementation:** In overlay mode, the YAML `IConfigurationProvider` is
registered early in the `IConfigurationBuilder` source list (low priority), so
that env vars and `appsettings.json` registered later take precedence. In strict
mode, a separate `IConfiguration` instance is built with only the YAML source,
and the `DelegatingOptionsFactory` delegates for OTel options types are wired to
read from that isolated instance rather than the root `IConfiguration`.

### 3.2 Configuration Change Debouncing

A file watcher or OpAMP client can fire changes in rapid succession. When
multiple `IConfiguration` keys are updated sequentially (e.g.,
`OTEL_TRACES_SAMPLER` then `OTEL_TRACES_SAMPLER_ARG` a few milliseconds later),
each key update calls `OnReload()`, which triggers `IOptionsMonitor` to
recompute options and fire `OnChange`. The result: two `OnChange` callbacks in
quick succession, the first seeing the new sampler type with the old arg, the
second seeing both new values. The first creates a sampler with a stale
argument.

The `TelemetryPolicyConfigurationProvider.UpdatePolicies()` in
[S4.4](configuration-analysis.md#44-telemetry-policies-architecture) mitigates
this by accepting a full dictionary and calling `OnReload()` once - all keys
update atomically. But this only helps when the source adapter batches updates
correctly.

**Remaining risks:**

| Source type | Batching behaviour | Risk |
| --- | --- | --- |
| `TelemetryPolicyConfigurationProvider` (OpAMP, file-based, or custom) | Single `UpdatePolicies` call per policy update | Low - adapter batches naturally |
| File-watching `IConfigurationProvider` (JSON/YAML) | Single `OnReload()` per file change event | Low - file is parsed atomically |
| `EnvironmentVariablesConfigurationProvider` | No reload - env vars are read once | None |
| User calling `IConfigurationRoot.Reload()` | Reloads all providers; single `IChangeToken` signal | Low - single event |
| Multiple `IConfigurationProvider` sources changing independently | Each fires its own `IChangeToken` | **Medium** - N change events for N sources |

The medium-risk scenario occurs when the application has multiple configuration
sources (e.g., YAML file + OpAMP-backed policy source) that both contribute OTel
keys and change at similar times. Each source fires its own `IChangeToken`,
producing multiple `IOptionsMonitor` recomputes.

**Mitigation:** The
[S3.3](#33-ioptionsmonitor-change-notification-granularity-thundering-herd)
change-detection guard (value equality check before expensive operations)
already handles this at the subscriber level - the second `OnChange` callback
detects that the values haven't changed since the first callback already applied
them, and short-circuits. No additional debouncing mechanism is needed in the
SDK as long as the value guard pattern is consistently applied.

### 3.3 IOptionsMonitor Change Notification Granularity (Thundering Herd)

`IOptionsMonitor<T>.OnChange` fires for **all** named instances of `T` when
**any** `IChangeToken` from **any** `IConfigurationProvider` fires. The callback
receives the name parameter, but every subscriber is invoked regardless of
whether their specific configuration keys changed.

The declarative config design ([Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories))
proposes position-based named options:
`declarative:sdk:traces:processors:0:batch:exporter:otlp`, `...:1:...`, etc. A
system with 10 named OTLP exporters where one endpoint changes triggers all 10
`OnChange` callbacks. Each calls `IOptionsMonitor.Get(name)`, which recomputes
the options via `DelegatingOptionsFactory.Create()`. Nine of them will get
identical options back, but still execute the full factory pipeline and, in the
[Deep Dive C.3](configuration-analysis-deep-dives.md#c3-solution-approaches)
design, potentially recreate their `HttpClient`.

**Required pattern - change detection guard:**

```csharp
optionsMonitor.OnChange((newOpts, name) =>
{
    if (this.disposed || name != this.optionsName) return;  // name filter (already in Deep Dive C.3)
    if (!HasMeaningfulChange(this.currentOptions, newOpts)) return;  // value guard
    // ... proceed with expensive swap
});

private static bool HasMeaningfulChange(OtlpExporterOptions current, OtlpExporterOptions incoming)
    => current.Endpoint != incoming.Endpoint
    || current.Headers != incoming.Headers
    || current.TimeoutMilliseconds != incoming.TimeoutMilliseconds;
```

The name filter (`name != this.optionsName`) is already present in the [Deep
Dive C.3](configuration-analysis-deep-dives.md#c3-solution-approaches) sketch.
The value guard is the missing piece - it short-circuits the expensive client
recreation when options haven't actually changed. For the `ReloadableSampler`,
the equivalent check is whether sampler type and arg are unchanged.

### 3.4 Multiple Provider Instances Sharing IConfiguration and IOptionsMonitor

If an application creates multiple `TracerProvider` instances (e.g., for
testing, for different subsystems, or in multi-host scenarios), they share the
same `IConfiguration` and `IOptionsMonitor<T>` from the DI container.

A policy change targeting one provider's sampler rate (via
`TelemetryPolicyConfigurationProvider`) affects **all** providers using
`IOptionsMonitor<SamplerOptions>`, because `IOptionsMonitor` is a singleton in
the DI container. The `OnChange` callback fires for all subscribers.

**Current mitigation - named options:** The SDK already uses named options for
exporters. If each provider uses a different options name, their `OnChange`
callbacks filter by name
([S3.3](#33-ioptionsmonitor-change-notification-granularity-thundering-herd))
and only the targeted provider applies the change.

**Gap:** `SamplerOptions`
([S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration))
does not currently support named options - it uses `Options.DefaultName`. If two
`TracerProviderSdk` instances share a DI container, both subscribe to
`IOptionsMonitor<SamplerOptions>.OnChange` and both will apply the same sampler
change. There is no mechanism to target a policy change at one provider but not
the other.

**Recommendation:** This is an uncommon scenario - most applications have one
`TracerProvider`. Document that multiple `TracerProviderSdk` instances in the
same DI container share sampler configuration. If isolation is needed, use
separate DI containers (separate `IHost` instances) or introduce named
`SamplerOptions` support (future work).

### 3.5 IConfigurationProvider Priority Ordering Determinism

When multiple `IConfigurationProvider` sources contribute the same key, the
**last registered source wins** (standard `IConfiguration` behaviour - later
sources override earlier ones). The declarative config design involves at least
three sources:

```csharp
// Registration order determines priority (last wins)
builder.Configuration
    .AddEnvironmentVariables()                          // 1. env vars (lowest priority in this example)
    .Add(new DeclarativeConfigYamlSource("otel.yaml"))  // 2. YAML file
    .Add(new TelemetryPolicyConfigurationSource());     // 3. Policy provider - e.g., OpAMP, file-based, custom (highest priority)
```

The expected priority for the .NET SDK (in overlay mode per
[S3.1](#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option)):

| Priority | Source | Rationale |
| --- | --- | --- |
| Lowest | Declarative config YAML file | Provides baseline/default configuration |
| Medium | Environment variables | Override YAML defaults per deployment |
| Medium-high | `appsettings.json` / user secrets | Override per environment |
| High | `Configure<T>()` delegates (programmatic) | Override everything (runs after IConfiguration) |
| Highest | `TelemetryPolicyConfigurationProvider` (OpAMP, file-based, or custom source) | Runtime policy overrides all static config |

The `DelegatingOptionsFactory` priority model
([S2.1](configuration-analysis.md#21-configuration-infrastructure)) already
handles the `Configure<T>` layer - programmatic delegates always override
`IConfiguration` values regardless of source ordering. The question is the
ordering **within** the `IConfiguration` layer.

**Risk:** If the YAML source is registered after env vars (as shown above), YAML
values override env vars. This is the opposite of what most users expect - env
vars are typically the highest-priority static source in .NET applications.

**Recommendation:** `UseDeclarativeConfiguration()` should register the YAML
`IConfigurationSource` at a **low priority position** in the builder (before env
vars and appsettings.json). This preserves the standard .NET expectation that
env vars override file-based config. The `TelemetryPolicyConfigurationProvider`
should be registered **last** so that runtime policy changes override all static
sources.

The
[S3.1](#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option)
`AllowConfigurationOverlay` option controls whether other sources participate at
all. When overlay is enabled, the ordering above applies. When overlay is
disabled (strict mode), only the YAML source and the policy provider are active
for OTel keys.

### 3.6 OTEL_SDK_DISABLED and Runtime Disable

`OTEL_SDK_DISABLED` is read once at provider build time
([S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration)).
If set to `true`, a noop provider is returned. There is no mechanism to
re-evaluate this flag at runtime.

#### Dynamic Source Subscription Is Out of Scope for Declarative Config

The OTel declarative config YAML spec defines pipeline configuration - samplers,
processors, exporters - but **does not** specify which `ActivitySource` names to
subscribe to. Source subscription (`AddSource()`) is a .NET-specific concern
driven by the `ActivityListener.ShouldListenTo` runtime API, not by the spec
YAML schema. The declarative config spec at
`sdk.traces.processors[].batch.exporter` controls how spans are processed and
exported, not which libraries produce them.

This means changing which sources are listened to is **not a declarative config
scenario**. It belongs to the telemetry policies domain (controlling which
instrumentation libraries are active at runtime, potentially via OpAMP or other
policy sources) and should be considered independently from the declarative
config work.

For the declarative config focus, source subscription changes should be
classified as **Tier 3 (restart required)** - the same category as protocol
changes and exporter selection. The `ShouldListenTo` predicate is evaluated once
per source-listener pair and cached by the runtime; changing it requires
rebuilding the `ActivityListener`, which effectively means rebuilding the
`TracerProviderSdk`.

#### Recommendation

| Concern | Decision | Rationale |
| --- | --- | --- |
| `OTEL_SDK_DISABLED` | Startup-only (current) | Controls noop vs real provider construction; not a runtime toggle |
| Source subscription changes (add/remove sources) | Tier 3 - restart required | Not in declarative config spec; `ShouldListenTo` is cached by runtime |
| Per-source runtime disable (dynamic) | Out of scope for declarative config | Belongs to telemetry policies domain |
| Global export disable/re-enable | L4 (export kill-switch, [S4.5](configuration-analysis.md#45-recommended-build-order) Tier 2) | Simple, already designed, covers the "stop all export" scenario |

### 3.7 Startup Ordering - IConfiguration Must Be Built Before Options Resolve

The `DelegatingOptionsFactory` resolves `IConfiguration` from DI at registration
time (`sp.GetRequiredService<IConfiguration>()`). The `TelemetryHostedService`
eagerly initialises providers during `StartAsync`, which triggers options
resolution.

The critical ordering requirement: the declarative config YAML
`IConfigurationProvider` must be registered with the `IConfigurationBuilder`
**before** `TelemetryHostedService.StartAsync` fires - otherwise the options
factories create instances without seeing the YAML values.

In the standard ASP.NET Core host builder, `IConfiguration` is built before
`IServiceProvider` is constructed, and `IHostedService` instances run after the
container is built. So the ordering is naturally correct:

```text
1. Host.CreateBuilder()           <-- IConfigurationBuilder available
2. .UseDeclarativeConfiguration() <-- registers YAML IConfigurationSource
3. builder.Build()                <-- IConfiguration built with YAML source included
4. host.RunAsync()                <-- TelemetryHostedService.StartAsync fires; options resolve with YAML values
```

**Risk scenario:** If someone calls `builder.Services.AddOpenTelemetry()` and
later adds a configuration source via `builder.Configuration.AddXxx()`, the
ordering is still correct because `IConfiguration` is built lazily from all
registered sources at container build time.

**Real risk:** Manual `ServiceCollection` usage (no host builder). The fallback
registration in `ProviderBuilderServiceCollectionExtensions`
(`TryAddSingleton<IConfiguration>(new
ConfigurationBuilder().AddEnvironmentVariables().Build())`) only includes env
vars. If someone registers a YAML `IConfigurationSource` but uses a manual
`ServiceCollection`, the YAML source is on the wrong `IConfigurationBuilder` --
the fallback `IConfiguration` ignores it.

**Recommendation:** `UseDeclarativeConfiguration()` should validate at
registration time that the `IServiceCollection` will produce an `IConfiguration`
that includes the YAML source. If the user is using a manual `ServiceCollection`
(no host builder), the extension method should register the YAML source into the
fallback `IConfiguration` explicitly.

---

## 4. Informational / Future Consideration

### 4.1 YAML Array-to-IConfiguration Projection

The declarative config YAML uses arrays for processors, exporters, and
propagators:

```yaml
processors:
  - batch:
      schedule_delay: 5000
  - simple: {}
propagators:
  - tracecontext
  - baggage
  - b3
```

`IConfiguration` represents arrays as indexed keys:
`processors:0:batch:schedule_delay`, `processors:1:simple`, `propagators:0`,
`propagators:1`, `propagators:2`. The YAML `IConfigurationProvider` must project
arrays into this indexed key format for standard `IConfiguration` consumers to
work.

There is **no precedent** in the current codebase for `IConfiguration` indexed
array binding. All collection values today are either:

- Comma-separated strings parsed manually (`OTEL_EXPORTER_OTLP_HEADERS`,
  `OTEL_RESOURCE_ATTRIBUTES`)
- Programmatic list population (`ProcessorFactories`, `UriPrefixes`)

This creates two design challenges:

**Challenge 1 - Scalar arrays (propagator names, exporter names):**

`OTEL_PROPAGATORS=tracecontext,baggage,b3` is a comma-separated string. The YAML
equivalent is a list of scalar values. Both need to produce the same result in
the component registry. The YAML provider should project scalar arrays as
indexed keys (`OTEL_PROPAGATORS:0` = `tracecontext`, etc.), but the SDK code
that reads `OTEL_PROPAGATORS` will need to handle both formats: a single
comma-separated string (env var) and an indexed sequence (YAML).

**Challenge 2 - Object arrays (processor/exporter config):**

The position-based naming scheme in [Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories)
depends on stable indices. If the YAML file is edited to reorder processors, the
named options keys change, which could cause `IOptionsMonitor` to treat the
reordered entries as "changed" even if the logical configuration is identical.

**Recommendation:** The YAML `IConfigurationProvider` implementation should
document the array projection format explicitly. For scalar arrays, provide a
helper method that reads both comma-separated and indexed formats:

```csharp
internal static string[] GetStringArray(IConfiguration configuration, string key)
{
    // Try indexed format first (YAML arrays): key:0, key:1, key:2
    var section = configuration.GetSection(key);
    var children = section.GetChildren().ToArray();
    if (children.Length > 0)
        return children.Select(c => c.Value!).Where(v => v is not null).ToArray();

    // Fall back to comma-separated format (env vars): "value1,value2,value3"
    var csv = configuration[key];
    return csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           ?? [];
}
```

### 4.2 Collection Properties Not Bindable from IConfiguration Sections

Several options properties have types that `IConfiguration.Bind()` cannot
populate from a flat key-value section, and the manual `IConfiguration[key]`
pattern used throughout the SDK cannot handle either:

| Options Class | Property | Type | Binding Status |
| --- | --- | --- | --- |
| `PrometheusHttpListenerOptions` | `UriPrefixes` | `IReadOnlyCollection<string>` | Not bindable - configured only via `Action<T>` delegates |
| `OtlpExporterOptions` | `Headers` | `string?` (comma-separated) | Bindable as raw string; parsing to key-value pairs happens later in `GetHeaders<T>()` |
| `OtlpExporterOptions` | `HttpClientFactory` | `Func<HttpClient>` | Not bindable - delegate type, programmatic only |
| `OpenTelemetryLoggerOptions` | `ProcessorFactories` | `List<Func<IServiceProvider, BaseProcessor<LogRecord>>>` | Not bindable - delegate collection, programmatic only (being removed per [S2.1](configuration-analysis.md#21-configuration-infrastructure)) |
| `ZipkinExporterOptions` | `HttpClientFactory` | `Func<HttpClient>` | Not bindable - delegate type |

Delegate properties (`HttpClientFactory`, `ProcessorFactories`) are inherently
non-declarative - they cannot be expressed in YAML or any configuration file.
This is expected and correct.

`UriPrefixes` is the interesting case: it is a legitimate declarative value (a
list of URI strings) that should be expressible in YAML but currently has no
`IConfiguration` binding path. The declarative config design needs to handle
this, likely via the `IConfigurationProvider` array projection
([S4.1](#41-yaml-array-to-iconfiguration-projection)) combined with explicit
`IConfiguration[key]`-based reading in the options constructor:

```csharp
// In PrometheusHttpListenerOptions constructor (new IConfiguration-aware overload)
internal PrometheusHttpListenerOptions(IConfiguration configuration)
{
    var prefixes = OpenTelemetryConfigurationExtensions.GetStringArray(configuration, "UriPrefixes");
    if (prefixes.Length > 0)
        this.UriPrefixes = prefixes;
}
```

**Recommendation:** Audit all options classes and categorise properties as: (a)
scalar-bindable, (b) collection- bindable (needs array projection), (c)
non-declarative (delegates, callbacks). Document category (c) properties as
programmatic-only in the declarative config guidance.

### 4.3 Named Options Cache Unbounded Growth

`IOptionsMonitor<T>` caches options instances in a `ConcurrentDictionary<string,
Lazy<TOptions>>` (in the internal `OptionsCache<T>`). Entries are never evicted.
The declarative config design ([Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories))
generates position-based names like
`declarative:sdk:traces:processors:0:batch:exporter:otlp`.

In a typical deployment, the number of named options is small and static (one
per configured exporter/processor). However, two scenarios can cause unbounded
growth:

1. **Dynamic declarative config reloading with structural changes:** If a YAML
   file reload adds or removes processors, the new positions generate new
   options names. The old names remain in the cache with stale instances that
   are never accessed again.

2. **Programmatic misuse:** Code that calls `IOptionsMonitor.Get(dynamicName)`
   with a per-request or per-trace name (e.g., using a trace ID as the options
   name) would grow the cache without bound.

Scenario 1 is the realistic concern. It would only manifest in long-running
processes with frequent structural config changes - unlikely in practice, but
worth documenting.

**Mitigation:** `OptionsMonitor<T>` exposes a `TryRemove(string name)` method
(added in .NET 8). When the declarative config system detects a structural
change (processor added/removed), it should call `TryRemove` for options names
that no longer correspond to active components. This is a cleanup concern for
the YAML config walker, not for individual components.

### 4.4 IConfiguration.GetSection() Returns Non-Null for Missing Sections

`IConfiguration.GetSection("nonexistent")` returns a valid
`IConfigurationSection` with `Value == null` and zero children - it never
returns `null`. This is a well-known .NET configuration gotcha that affects the
factory `Create` methods.

In the component registry design ([Deep Dive
E.2](configuration-analysis-deep-dives.md#e2-factory-interface-design)), each
factory's `Create` method receives an `IConfiguration` subtree for its YAML
node:

```csharp
public BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services)
```

If the YAML node is malformed or empty, `configuration` is a valid but vacuous
`IConfigurationSection`. The factory must handle this gracefully - falling back
to defaults rather than producing a partially-configured component.

The SDK's existing `OpenTelemetryConfigurationExtensions` helpers
(`TryGetStringValue`, `TryGetIntValue`, etc.) already handle this correctly:
they check for `null`/whitespace and return `false` when the key is absent.
Options constructors that use these helpers are safe.

The risk is in vendor-supplied factories ([Deep Dive
E.8](configuration-analysis-deep-dives.md#e8-third-party-extensibility)) that
use raw `configuration["key"]` access without null checks, or that assume the
section has children:

```csharp
// Unsafe vendor pattern:
var endpoint = configuration["endpoint"];
options.Endpoint = new Uri(endpoint);  // NullReferenceException or UriFormatException if key absent
```

**Recommendation:** The `ISpanExporterFactory` / `ISamplerFactory` interface
documentation should explicitly state that the `IConfiguration` parameter may be
empty (no keys, no children) and that factories must handle this by using
default values. The SDK's `OpenTelemetryConfigurationExtensions` helpers should
be documented as the recommended approach for safe key reads, available for
vendor use.

### 4.5 Keyed Services (.NET 8+) as Future Simplification

.NET 8 introduced [keyed
services](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#keyed-services)
in `Microsoft.Extensions.DependencyInjection`. The named options pattern used
throughout the SDK (`IOptionsMonitor<T>.Get(name)`) and the component registry
design (`IEnumerable<ISpanExporterFactory>` with `Name` matching) both solve
problems that keyed services address natively:

| Current pattern | Keyed services equivalent |
| --- | --- |
| `IOptionsMonitor<OtlpExporterOptions>.Get("myExporter")` | `[FromKeyedServices("myExporter")] OtlpExporterOptions options` |
| `IEnumerable<ISpanExporterFactory>` + `Name == "otlp"` lookup | `sp.GetRequiredKeyedService<ISpanExporterFactory>("otlp")` |

Keyed services offer several advantages:

1. **Direct DI resolution** - no enumeration and linear search for the matching
   `Name`
2. **Standard DI diagnostics** - keyed registrations appear in DI container
   dumps
3. **Compile-time key references** - keys can be constants rather than magic
   strings matched at runtime
4. **Constructor injection** - components can declare their keyed dependency
   directly rather than resolving from `IServiceProvider`

However, adopting keyed services today is premature:

- The SDK's minimum supported TFM includes `net8.0`, where keyed services are
  available, but the shared infrastructure must also work on
  `netstandard2.0`/`net462` (where they are not)
- The named options pattern is deeply established and well-understood by
  contributors
- The component registry design ([Deep Dive
  E](configuration-analysis-deep-dives.md#e-component-registry-detailed-design))
  is self-contained - it can be migrated to keyed services later without public
  API changes because the `IEnumerable<IFactory>` resolution is internal

**Recommendation:** Document keyed services as a future simplification target.
When the SDK drops `netstandard2.0` support (or when keyed services are
backported to the `Microsoft.Extensions.*` packages for older TFMs), the
component registry and named options resolution can be migrated. The current
design should avoid patterns that would make this migration harder (e.g., don't
expose `IEnumerable<ISpanExporterFactory>` as public API if keyed resolution
would replace it).

### 4.6 Testing Infrastructure for Reload Scenarios

No test infrastructure exists in the current codebase for verifying
configuration reload correctness. The existing test patterns use:

- Direct options construction with known values
- `Configure<T>` delegates in test DI containers
- Static env var setup via `IConfiguration` builders

Testing the reload path requires:

1. **A controllable `IConfigurationProvider`** that can be triggered to fire
   `OnReload()` on demand with specified new values - essentially a test-double
   version of `TelemetryPolicyConfigurationProvider`.

2. **Synchronisation primitives** to wait for `OnChange` callbacks to complete
   before asserting state - because `OnChange` callbacks may fire asynchronously
   ([S2.8](#28-onchange-callback-threading-model)) and the `IOptionsMonitor`
   recompute is lazy.

3. **Thread-safety assertions** to verify that concurrent span creation / log
   recording / metric collection during a reload does not produce exceptions or
   corrupt state.

4. **Scenario-specific test patterns:**

| Scenario | Test approach |
| --- | --- |
| Valid reload (value change) | Trigger provider with new values; assert component reflects new state |
| Invalid reload (validation failure) | Trigger provider with bad values; assert old state retained, EventSource warning logged |
| Rapid successive reloads | Trigger N reloads in tight loop; assert final state is consistent and no exceptions |
| Reload during active export | Start a slow export (mock exporter with delay); trigger reload mid-export; assert no crash |
| Reload after provider dispose | Dispose provider; trigger reload; assert no callback side-effects |

**Recommendation:** Build a `TestConfigurationProvider` (extending
`ConfigurationProvider`) as shared test infrastructure. This should be the first
deliverable alongside step 2 in the
[S4.5](configuration-analysis.md#45-recommended-build-order) build order
(`TelemetryPolicyConfigurationProvider`), since the production and test
providers share the same shape.

### 4.7 Configuration System Self-Observability

When configuration changes at runtime (sampling rate, export interval,
endpoint), operators need visibility into what changed, when, and whether the
change was applied or rejected. The current EventSource infrastructure
(`OpenTelemetrySdkEventSource`, `OpenTelemetryProtocolExporterEventSource`)
supports this, but the reload design doesn't specify what events to emit.

**Recommended events:**

| Event | Severity | Data | When |
| --- | --- | --- | --- |
| `ConfigurationChangeDetected` | Informational | Options type name, options name, source (file/OpAMP/manual) | `OnChange` callback entered |
| `ConfigurationChangeApplied` | Informational | Options type name, options name, key properties that changed | After successful swap |
| `ConfigurationChangeRejected` | Warning | Options type name, options name, reason (validation failure, exception message) | After catch in `OnChange` |
| `ConfigurationChangeSkipped` | Verbose | Options type name, options name | When value-equality guard ([S3.3](#33-ioptionsmonitor-change-notification-granularity-thundering-herd)) determines no meaningful change |

These events directly support the [S2.3](#23-onchange-callback-exception-safety)
(exception safety) and
[S3.3](#33-ioptionsmonitor-change-notification-granularity-thundering-herd)
(thundering herd) patterns - the try/catch and value-guard branches each emit a
specific event.

For higher-level observability, the SDK could also emit OpenTelemetry metrics
about its own configuration:

- `otel.sdk.config.reload.count` (counter, tagged by options type and outcome:
  applied/rejected/skipped)
- `otel.sdk.config.reload.duration` (histogram, tagged by options type --
  measures time in `OnChange` callback)

These are lower priority than the EventSource events but valuable for fleets
managed via telemetry policies (e.g., OpAMP-backed deployments).
