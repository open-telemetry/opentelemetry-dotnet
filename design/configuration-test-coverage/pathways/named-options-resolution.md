# Named-Options Resolution - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/Shared/Options/DelegatingOptionsFactory.cs:83` - `Create(string name)`:
  for `IConfigureNamedOptions<T>`, calls `namedSetup.Configure(name, options)`.
  For unnamed `IConfigureOptions<T>`, calls `setup.Configure(options)` only when
  `name == Options.DefaultName`. `PostConfigure<T>` always fires regardless of
  name.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:192` -
  `CreateOtlpExporterOptions(IServiceProvider, IConfiguration, string name)`:
  factory used by `AddOtlpExporter`. `configurationType` is always
  `OtlpExporterOptionsConfigurationType.Default`; signal-specific env vars
  (`OTEL_EXPORTER_OTLP_TRACES_*` etc.) are never read via this factory.
  The `name` argument is used only to resolve `BatchExportActivityProcessorOptions`.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:22` -
  `UseOtlpExporter` pathway. `RegisterOptionsFactory` registers
  `OtlpExporterBuilderOptions` under the builder's name (default `"otlp"` when
  a `name`/`IConfiguration` pair is supplied, else `Options.DefaultName`).
  The builder options constructor creates three signal-specific `OtlpExporterOptions`
  instances with the correct `OtlpExporterOptionsConfigurationType`, so
  signal-specific env vars DO apply in this pathway.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:62` -
  `AddOtlpExporter(name, configure)`: when `name != null`, resolves via
  `IOptionsMonitor<OtlpExporterOptions>.Get(finalOptionsName)` (named options
  cache). When `name == null`, calls `IOptionsFactory<OtlpExporterOptions>.Create()`
  to produce a fresh instance every time, then invokes the delegate inline.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporterExtensions.cs:82` -
  mirrors the trace helper: named path uses `IOptionsMonitor.Get(name)`;
  unnamed path creates a fresh instance.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporterHelperExtensions.cs:199` -
  same pattern; `LogRecordExportProcessorOptions` also resolved via
  `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(finalOptionsName)`.

Named-options call sites in scope (grep: `IOptionsMonitor.*\.Get\(`):

- `OtlpTraceExporterHelperExtensions`: `OtlpExporterOptions`, `ExperimentalOptions`
- `OtlpMetricExporterExtensions`: `OtlpExporterOptions`, `MetricReaderOptions`,
  `ExperimentalOptions`
- `OtlpLogExporterHelperExtensions`: `OtlpExporterOptions`,
  `LogRecordExportProcessorOptions`, `ExperimentalOptions`
- `OtlpExporterBuilder`: `OtlpExporterBuilderOptions`, `ExperimentalOptions`,
  `LogRecordExportProcessorOptions`, `MetricReaderOptions`,
  `ActivityExportProcessorOptions`
- `ProviderBuilderServiceCollectionExtensions`: `BatchExportLogRecordProcessorOptions`,
  `PeriodicExportingMetricReaderOptions`, `BatchExportActivityProcessorOptions`

## 1. Existing coverage

Section 1 is facts-only; no gap marking.

| File:method | Scenario summary | Observation | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpTraceExporterTests.NamedOptionsMutateSeparateInstancesTest` | Named options produce separate instances per name | DirectProperty | [Collection] |
| `OtlpTraceExporterTests.NonnamedOptionsMutateSharedInstanceTest` | Unnamed options share the same cached instance | DirectProperty | [Collection] |
| `OtlpTraceExporterTests.AddOtlpTraceExporterNamedOptionsSupported` | `AddOtlpExporter(name, configure)` wires named options pipeline | DI | [Collection] |
| `OtlpLogExporterTests.AddOtlpExporterWithNamedOptions` | Named options for log exporter | DI | none noted |
| `OtlpMetricsExporterTests.TestAddOtlpExporter_NamedOptions` | Named options for metrics exporter | DI | [Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | `UseOtlpExporter` with named `Configure<T>` delegate (Theory) | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsTest` | `OTEL_EXPORTER_OTLP_*` env vars applied in UseOtlpExporter path | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | Same scenario via injected IConfiguration instead of process env vars | DI | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterDefaultTest` | Default UseOtlpExporter produces correct OtlpExporterBuilderOptions | DI | Class-IDisposable+[Collection] |

## 2. Scenario checklist and gap analysis

### 2.1 Default name (Options.DefaultName) vs signal-specific named instances

The `AddOtlpExporter` helper always creates options with
`OtlpExporterOptionsConfigurationType.Default`; signal-specific env vars are
not read, regardless of the `name` argument. The `UseOtlpExporter` helper
creates three separate `OtlpExporterOptions` instances (one per signal) inside
`OtlpExporterBuilderOptions`, each with the correct `configurationType`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `AddOtlpExporter()` (unnamed) reads only generic env vars | `OtlpExporterOptions_EnvironmentVariableOverride` (Default type rows) | Generic vars read; signal-specific vars ignored | covered |
| `AddOtlpExporter(name, cfg)` reads only generic env vars (not signal-specific) | none | Same as unnamed; signal-specific vars always ignored in this pathway | missing |
| `UseOtlpExporter` reads signal-specific env vars per signal | `UseOtlpExporterRespectsSpecEnvVarsTest` | Signal-specific vars read and applied | covered |
| Options resolved under `Options.DefaultName` differ from those under a named key | `NonnamedOptionsMutateSharedInstanceTest` / `NamedOptionsMutateSeparateInstancesTest` | Separate instances; mutations do not cross names | covered |

### 2.2 Signal-specific env vars applied only in UseOtlpExporter pathway

`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`, `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`, and
`OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` are read only when
`OtlpExporterOptionsConfigurationType` matches the signal. This is available
only in the `UseOtlpExporter` pathway.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Signal-specific endpoint env var applies in UseOtlpExporter - traces | `UseOtlpExporterRespectsSpecEnvVarsTest` | Traces endpoint applied | covered |
| Signal-specific endpoint env var applies in UseOtlpExporter - metrics | `UseOtlpExporterRespectsSpecEnvVarsTest` | Metrics endpoint applied | covered |
| Signal-specific endpoint env var applies in UseOtlpExporter - logs | `UseOtlpExporterRespectsSpecEnvVarsTest` | Logs endpoint applied | covered |
| Signal-specific env var is NOT applied in AddOtlpExporter pathway | none | Generic endpoint used; signal-specific var silently ignored | missing |
| Signal-specific via IConfiguration in UseOtlpExporter | `UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | IConfiguration used as source | covered |

The missing test is important: a user who sets
`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` and uses `AddOtlpExporter` will not see
the expected endpoint because the generic `OTEL_EXPORTER_OTLP_ENDPOINT` is used
instead. This behavioral difference is currently undocumented in tests.

### 2.3 Named instance state - shared vs separate

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Two different names produce independent instances | `NamedOptionsMutateSeparateInstancesTest` | Separate instances; no state bleed | covered |
| Unnamed options share one cached instance | `NonnamedOptionsMutateSharedInstanceTest` | Same instance returned | covered |
| Named `Configure<T>(name, delegate)` applies only to matching name | none | By DI contract; not verified for OTLP options | missing |
| Unnamed `Configure<T>(delegate)` (no name) applies to DefaultName only | none | Factory skips unnamed setups for non-default names | missing |

The factory's `Create(string name)` loop applies an unnamed `IConfigureOptions<T>`
only when `name == Options.DefaultName` (line 89 of `DelegatingOptionsFactory.cs`).
No test verifies this for any OTLP options class.

### 2.4 Configure<T>(name) vs Configure<T>() (no name)

`Configure<T>(name, delegate)` registers an `IConfigureNamedOptions<T>`, which
the factory applies for every resolved name. `Configure<T>(delegate)` (no name)
registers an `IConfigureOptions<T>`, applied only when name is `Options.DefaultName`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Named configure applies to matching name only | `UseOtlpExporterConfigureTest` (uses named configure) | Named delegate scoped to name | partial |
| Unnamed configure does NOT apply to non-default-name resolution | none | Factory omits unnamed setups for non-default names | missing |
| Named configure applies to ALL names (IConfigureNamedOptions fires for every name) | none | IConfigureNamedOptions.Configure(name, options) fires per name | missing |

`UseOtlpExporterConfigureTest` tests named configure in the UseOtlpExporter
context but does not assert that the delegate does NOT apply under a different
name. The partial rating reflects that positive coverage exists; negative
isolation is untested.

### 2.5 UseOtlpExporter vs AddOtlpExporter configuration parity

Both pathways can produce `OtlpExporterOptions` for tracing, metrics, and
logging. Given the same generic env var inputs (`OTEL_EXPORTER_OTLP_ENDPOINT`
etc.) and no signal-specific env vars, the effective endpoint resolved through
both pathways should be equivalent.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Same generic env var input -> same effective endpoint via AddOtlpExporter | `OtlpExporterOptions_EnvironmentVariableOverride` (default type) | Generic vars applied | covered |
| Same generic env var input -> same effective endpoint via UseOtlpExporter | `UseOtlpExporterRespectsSpecEnvVarsTest` (no signal-specific set) | Generic vars used as fallback via ApplyDefaults | partial |
| Parity check: AddOtlpExporter and UseOtlpExporter produce identical options when only generic vars set | none | Not verified end-to-end | missing |
| UseOtlpExporter overrides only the matching signal's options via ApplyDefaults | none | `ApplyDefaults` merges signal-specific over generic; not exercised in isolation | missing |

## 3. Recommendations

### R1: Signal-specific env var ignored by AddOtlpExporter

- **Target test:**
  `OtlpExporterOptions_SignalSpecificEnvVar_IsIgnored_InAddOtlpExporterPathway`
- **Location:** `OtlpExporterOptionsTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 1 (direct construction with `OtlpExporterOptionsConfigurationType.Default`)
- **Observation:** `DirectProperty` - construct `OtlpExporterOptions` with
  `OtlpExporterOptionsConfigurationType.Default` and an IConfiguration that
  contains `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`. Assert the endpoint remains at
  the default (signal-specific key not read in Default mode).
- **Guards issues:** 14, 23
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #14 (OTLP component factories use named
// options; factories must resolve pre-bound named options via IOptionsMonitor).
// Guards risks: 1.1.
// Observation: DirectProperty - Default configurationType; signal-specific key ignored.
// Coverage index: pathway.named-options-resolution.env-var.signal-specific-not-in-add-otlp
```

- **Risk vs reward:** Low effort (Tier 1 direct construction). Pins a behavioral
  difference between the two OTLP pathways that is unintuitive to users and
  could silently regress if `configurationType` selection logic changes.

### R2: Named Configure<T> does not affect different name

- **Target test:**
  `OtlpExporterOptions_NamedConfigure_DoesNotAffect_DifferentName`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - register `Configure<OtlpExporterOptions>("name-a", ...)`.
  Resolve `IOptionsMonitor<OtlpExporterOptions>.Get("name-b")`. Assert the
  delegate value does NOT appear on the "name-b" instance.
- **Guards issues:** 14, 23
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - IOptionsMonitor.Get("name-b"); named configure for "name-a"
// must not bleed into other names.
// Coverage index: pathway.named-options-resolution.configure-t.named-vs-unnamed-isolation
```

- **Risk vs reward:** Low effort. Verifies a DI contract that, if broken, would
  cause subtle cross-signal configuration bleed in multi-signal deployments.

### R3: Unnamed Configure<T> does not apply to non-default-name resolution

- **Target test:**
  `OtlpExporterOptions_UnnamedConfigure_NotApplied_ForNonDefaultName`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - register `Configure<OtlpExporterOptions>(opts => ...)`.
  Resolve `IOptionsMonitor<OtlpExporterOptions>.Get("custom-name")`. Assert the
  unnamed delegate value does NOT appear because the name is not `Options.DefaultName`.
- **Guards issues:** 14
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - unnamed IConfigureOptions<T> skipped for non-DefaultName.
// Coverage index: pathway.named-options-resolution.configure-t.unnamed-skipped-for-named
```

- **Risk vs reward:** Low effort. Documents the `DelegatingOptionsFactory.Create`
  factory behavior at line 89 that is not obvious and has no existing test.

### R4: Parity check - AddOtlpExporter vs UseOtlpExporter with generic env vars

- **Target test:**
  `OtlpExporter_AddVsUsePathway_ProduceSameOptions_WhenOnlyGenericEnvVarsSet`
- **Location:** `UseOtlpExporterExtensionTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - configure a service collection using both pathways
  with identical `OTEL_EXPORTER_OTLP_ENDPOINT` values (via injected
  IConfiguration). Resolve the effective endpoint for the tracing signal from
  both. Assert both produce the same endpoint.
- **Guards issues:** 14, 23
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #23 (OTLP exporter reload wiring uses named
// instances; reload path must resolve through the same named-options graph).
// Guards risks: 1.1.
// Observation: DI - compare effective OtlpExporterOptions.Endpoint for both pathways.
// Coverage index: pathway.named-options-resolution.parity.add-vs-use-otlp
```

- **Risk vs reward:** Medium effort (two pipeline setups in one test). High guard
  value: any divergence in env-var handling between pathways will surface here
  before Issues 14 and 23 rework the factory wiring.

### R5: Signal-specific env var beats generic in UseOtlpExporter - per signal

The `UseOtlpExporterRespectsSpecEnvVarsTest` covers the broad case.
A complementary test should verify the fallback direction (generic used when
signal-specific absent) for each signal independently.

- **Target test:**
  `UseOtlpExporter_SignalSpecificEnvVarBeatsGeneric_PerSignal` (Theory over
  traces/metrics/logs)
- **Location:** `UseOtlpExporterExtensionTests.cs`
- **Tier:** 2
- **Observation:** `DI` - for each signal, set both generic and signal-specific
  endpoint keys to different values. Assert signal-specific wins. Then verify
  fallback: set only generic, assert generic used for the signal.
- **Guards issues:** 14, 23
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #14 (factory named-options rework).
// Guards risks: 1.1.
// Observation: DI - OtlpExporterBuilderOptions per-signal resolution.
// Coverage index: pathway.named-options-resolution.env-var.signal-specific-in-use-otlp
```

- **Risk vs reward:** Low-to-medium effort (Theory over three signals). Covers
  the `ApplyDefaults` fallback logic that is the primary reason `UseOtlpExporter`
  and `AddOtlpExporter` behave differently with signal-specific env vars.

## Guards issues

- **Issue 14** - OTLP exporter component factories use named options: the factories
  must resolve pre-bound named options via `IOptionsMonitor<T>.Get(name)`. The
  named-isolation tests in Section 3 are prerequisites for verifying that the
  reworked factories resolve the correct instance.
- **Issue 23** - OTLP exporter reload wiring uses named instances: reload logic
  must fire `OnChange` on the correct named instance. The parity and per-signal
  tests in Section 3 establish the baseline that must hold after reload wiring
  is added.
