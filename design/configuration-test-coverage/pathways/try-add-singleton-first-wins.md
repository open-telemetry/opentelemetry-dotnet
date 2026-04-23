# Try-Add-Singleton First-Wins - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs:24` -
  `RegisterOptionsFactory<T>` (both overloads) calls
  `services.TryAddSingleton<IOptionsFactory<T>>(...)`. `TryAddSingleton` is a
  no-op if any registration for `IOptionsFactory<T>` is already present. First
  caller wins; all subsequent callers are silently skipped.
- `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs:67` -
  `DisableOptionsReloading<T>` calls
  `services.TryAddSingleton<IOptionsMonitor<T>, SingletonOptionsManager<T>>()`.
  Same first-wins behavior for the monitor.
- `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs` -
  `RegisterOptionsFactory` call sites in scope (all three provider builders):
  `BatchExportActivityProcessorOptions` (line 53), `ActivityExportProcessorOptions`
  (line 54), `BatchExportLogRecordProcessorOptions` (line 23),
  `LogRecordExportProcessorOptions` (line 24), `PeriodicExportingMetricReaderOptions`
  (line 38), `MetricReaderOptions` (line 39).
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:54` -
  three `RegisterOptionsFactory` calls for `OtlpExporterOptions`,
  `ExperimentalOptions`, and `SdkLimitOptions` (conditional).
  `AddOtlpExporterSharedServices` is called from all three OTLP helper
  extensions (trace, log, metrics) and from `UseOtlpExporter`; the second and
  subsequent calls are silently skipped because the factories are already
  registered from the first call. This is the **intentional** idempotency path.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:176` -
  `RegisterOptionsFactory<OtlpExporterBuilderOptions>` for the `UseOtlpExporter`
  pathway. Called once per `UseOtlpExporter` invocation; subsequent duplicate
  calls are blocked earlier by `UseOtlpExporterRegistration` (not by TryAdd).
- Risk register: `configuration-analysis-risks.md` section 1.3 -
  "TryAddSingleton First-Wins / Silent Misconfiguration Risk". User code that
  registers a custom `IOptionsFactory<T>` before calling SDK extension methods
  causes the SDK's `DelegatingOptionsFactory` to be silently skipped.

## 1. Existing coverage

Section 1 is facts-only; no gap marking.

| File:method | Scenario summary | Observation | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterMultipleCallsTest` | Second `UseOtlpExporter` call throws `NotSupportedException` | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterLoggingTest` | `UseOtlpExporter` + `AddOtlpExporter` conflict (logging) | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterMetricsTest` | Same conflict for metrics | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterTracingTest` | Same conflict for tracing | DI | Class-IDisposable+[Collection] |

Note: all four tests exercise conflict detection via `UseOtlpExporterRegistration`
sentinel objects (`AddSingleton`, not `TryAddSingleton`). None exercises the
`TryAddSingleton` no-op path for `IOptionsFactory<T>` registrations.

No existing test registers a custom `IOptionsFactory<T>` before calling any SDK
extension and verifies (or fails to verify) which factory runs.

## 2. Scenario checklist and gap analysis

### 2.1 User-registered IOptionsFactory<T> before SDK extension

If application code calls `services.AddSingleton<IOptionsFactory<OtlpExporterOptions>>(myFactory)`
before calling `AddOtlpExporter()`, `RegisterOptionsFactory` inside the SDK
performs a `TryAddSingleton` that no-ops. The SDK's `DelegatingOptionsFactory`
is never registered. `myFactory` lacks:

- The env-var-reading constructor (`IConfiguration`-first approach).
- Signal-specific `OtlpExporterOptionsConfigurationType` wiring.
- Named-options wiring (passes empty `name` to user factory regardless of
  the name requested).
- `Configure<T>` delegate chaining from the DI pipeline.

The resultant options object appears valid but produces wrong values silently.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Custom `IOptionsFactory<OtlpExporterOptions>` registered before `AddOtlpExporter` - SDK factory skipped | none | SDK `DelegatingOptionsFactory` not registered; custom factory used | missing |
| Custom factory lacks env-var reading; env vars silently ignored | none | No env-var-sourced values appear on resolved options | missing |
| Custom factory used despite subsequent `AddOtlpExporter` registration | none | TryAdd skips silently; no error, no log | missing |

### 2.2 Multiple Configure<T> registrations are additive (not TryAdd)

`Configure<T>` uses `services.AddTransient<IConfigureOptions<T>>` (via
`Microsoft.Extensions.Options.OptionsServiceCollectionExtensions.Configure`),
which is not a `TryAdd`. All `Configure<T>` registrations accumulate and run in
registration order. This is the correct behavior and is the reason the
first-caller-wins risk is scoped to `IOptionsFactory<T>` only.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Two `Configure<T>` delegates for the same type both run | `ServiceCollectionAddOpenTelemetryConfigureActionTests` (6 cases) | Both delegates execute; second wins on the same property | covered |
| `Configure<T>` after `AddOtlpExporter` still takes effect | `OtlpExporterOptions_SetterOverridesEnvironmentVariable` (indirect) | Configure<T> runs after factory; effective | partial |

The partial rating for the second row reflects that the existing test uses a
direct setter (not a `Configure<T>` delegate through the DI pipeline), so the
DI flow is not fully exercised.

### 2.3 Intentional idempotency - repeated SDK extension calls

`AddOtlpExporterSharedServices` is intentionally idempotent: calling
`AddOtlpExporter` for traces, metrics, and logs each invoke
`AddOtlpExporterSharedServices`, but the factory for `OtlpExporterOptions` is
registered by the first call; subsequent calls' `TryAddSingleton` are no-ops.
This is correct by design; the shared factory is the same for all signals in
the `AddOtlpExporter` pathway.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `AddOtlpExporter` for all three signals shares one `OtlpExporterOptions` factory | `OtlpExporterOptions_EnvironmentVariableOverride` (Default type, indirect) | Single factory registration; correct | partial |
| Second `AddOtlpExporter(same-name)` call does not duplicate factory | none | TryAdd skips second; single factory remains | missing |

The partial rating for the first row reflects that the test exercises the shared
factory behavior through options resolution, but does not assert the registration
count.

### 2.4 Observable behavior when the SDK factory is silently skipped

Today there is no EventSource event, no log, and no exception when
`RegisterOptionsFactory` no-ops because a factory was already registered.
The only detectable symptom is that resolved options lack env-var values and
the `Configure<T>` chain is not invoked through the `DelegatingOptionsFactory`
(though `Configure<T>` registrations in DI still run through the stock
`OptionsFactory`, if one was injected instead).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| No diagnostic emitted when `RegisterOptionsFactory` no-ops | none | Silent; nothing observable today | missing |
| EventSource event emitted when factory registration is skipped (post-Issue 6) | none | Issue 6 not implemented; nothing to assert today | missing |

The second row is a forward-looking baseline: once Issue 6 implements
EventSource logging for the skip, a test can assert on the emitted event.
Today the test is `missing` because there is nothing to assert. The
`EventSource` observation mechanism (entry doc Sec.2.7) and the shared
`InMemoryEventListener` helper will be the right tools once the issue lands.

## 3. Recommendations

### R1: Custom IOptionsFactory<T> before SDK - silent skip verified

- **Target test:**
  `OtlpExporterOptions_CustomFactory_RegisteredFirst_SdkFactorySkipped`
- **Location:** `OtlpExporterOptionsTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - register a custom `IOptionsFactory<OtlpExporterOptions>`
  that returns a sentinel options instance. Call `AddOtlpExporter()`. Resolve
  `IOptions<OtlpExporterOptions>` and assert the sentinel values are present
  (proving the custom factory ran, not the SDK factory).
- **Guards issues:** 6
- **Risks pinned:** 1.3
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #6 (diagnostic logging for silent skip -
// the EventSource should emit when RegisterOptionsFactory is a no-op).
// Guards risks: 1.3.
// Observation: DI - custom IOptionsFactory<OtlpExporterOptions>; sentinel value
// present on resolved options proves SDK factory was skipped.
// Coverage index: pathway.try-add-singleton-first-wins.user-factory.silent-skip
```

- **Risk vs reward:** Low effort to write (simple DI registration test). The
  highest guard value in this pathway: pins the current behavior so that when
  Issue 6 adds a diagnostic, the test can be extended to also assert the
  EventSource event. Without this test, silent skips are completely invisible
  in CI.

### R2: Multiple Configure<T> are additive through DI pipeline

- **Target test:**
  `OtlpExporterOptions_MultipleConfigureT_AllRun_InRegistrationOrder`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - register three `Configure<OtlpExporterOptions>`
  delegates that each append a token to a mutable list. Resolve
  `IOptions<OtlpExporterOptions>` and assert all three tokens are present in
  registration order.
- **Guards issues:** 6
- **Risks pinned:** 1.3
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - multiple Configure<T> delegates accumulate; TryAdd does not
// apply to IConfigureOptions<T> registrations.
// Coverage index: pathway.try-add-singleton-first-wins.configure-t.additive-not-try
```

- **Risk vs reward:** Low effort. Establishes that the `TryAdd` risk is bounded
  to `IOptionsFactory<T>` only and does not affect `Configure<T>` delegation.

### R3: Repeated AddOtlpExporter calls share one factory (intentional idempotency)

- **Target test:**
  `AddOtlpExporter_CalledMultipleTimes_SingleFactoryRegistration_IsIdempotent`
- **Location:** `OtlpExporterHelperExtensionsTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - call `AddOtlpExporter` twice on the same
  `IServiceCollection`. Resolve `IOptions<OtlpExporterOptions>` and assert the
  result is the same instance (or structurally identical), confirming the factory
  registered by the first call is used.
- **Guards issues:** 6
- **Risks pinned:** 1.3
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - second AddOtlpExporter call no-ops RegisterOptionsFactory;
// single factory governs both resolutions.
// Coverage index: pathway.try-add-singleton-first-wins.idempotent.second-call-noop
```

- **Risk vs reward:** Low effort. Pins the intentional idempotency contract and
  distinguishes it from the accidental silent-skip in R1.

### R4: Forward baseline - EventSource emitted on silent skip (Issue 6 gate)

This test cannot be written today (nothing is emitted). It is documented here as
a planned-but-blocked baseline so that when Issue 6 is implemented the test
author has a clear target.

- **Target test:**
  `OtlpExporterOptions_CustomFactory_RegisteredFirst_EmitsEventSourceDiagnostic`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 2
- **Observation:** `EventSource` - attach `InMemoryEventListener` before building
  the service collection. Register a custom factory first, then call
  `AddOtlpExporter`. Resolve options. Assert that the expected EventSource event
  (event ID to be defined by Issue 6) was emitted.
- **Guards issues:** 6
- **Risks pinned:** 1.3
- **Code-comment hint:**

```csharp
// BASELINE: placeholder - not yet implementable.
// Expected to be activated under Issue #6 (diagnostic logging for
// RegisterOptionsFactory silent skip).
// Guards risks: 1.3.
// Observation: EventSource - InMemoryEventListener asserts on emitted event ID.
// Coverage index: pathway.try-add-singleton-first-wins.user-factory.event-source-diagnostic
```

- **Risk vs reward:** Zero effort today; high guard value once Issue 6 ships.
  Documents the test contract so Issue 6 implementors know exactly what to emit.

## Guards issues

- **Issue 6** - Diagnostic logging for the `RegisterOptionsFactory` silent skip:
  when the SDK's factory is silently skipped because a prior registration exists,
  an EventSource event should be emitted. The test for Issue 6 must assert what
  the EventSource emits when the skip occurs. Today there is nothing to emit and
  nothing to assert (R4 above). The baseline test in R1 pins the silent behavior
  so the delta from Issue 6 is visible.
