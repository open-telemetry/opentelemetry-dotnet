# ActivityExportProcessorOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration -
  `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs:12`.
- Public parameterless constructor (delegates to internal constructor with
  `new BatchExportActivityProcessorOptions()`) -
  `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs:19-22`.
- Internal constructor that accepts a pre-built
  `BatchExportActivityProcessorOptions` (used by the factory) -
  `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs:24-32`.
- Property declarations -
  `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs`:
  - `ExportProcessorType` (default `ExportProcessorType.Batch`) - line 37.
  - `BatchExportProcessorOptions` (getter/setter; setter throws `Guard.ThrowIfNull`
    on `null`) - lines 42-50.
- No env-var reads inside this class. The class has no constructor that
  reads `IConfiguration` or env vars directly. Env-var-backed values flow
  in only because the factory populates `BatchExportActivityProcessorOptions`
  (which does read env vars) and passes it to the internal constructor.
- Options factory registration (tracing branch of
  `AddOpenTelemetryTracerProviderBuilderServices`) -
  `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:54-56`.
  The factory calls
  `new ActivityExportProcessorOptions(IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(name))`.
- `IConfiguration` binding for the `UseOtlpExporter` pathway -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:161-162`.
  Calls `services.Configure<ActivityExportProcessorOptions>(name,
  configuration.GetSection("TracingOptions"))`.
- `ConfigureTracingProcessorOptions` builder method that exposes
  `Action<ActivityExportProcessorOptions>` to callers -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:111-118`.

### Direct consumer sites

- `OtlpExporterBuilderOptions` constructor reads
  `ActivityExportProcessorOptions.BatchExportProcessorOptions` to seed the
  default batch options shared across all four `OtlpExporterOptions` instances -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:19,42-44`.
- `OtlpExporterBuilder` (UseOtlpExporter path) reads both properties and
  passes them to
  `OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:230,237-238`.
- `OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor` (full overload)
  branches on `exportProcessorType`:
  `Simple` -> `SimpleActivityExportProcessor`;
  `Batch` -> `BatchActivityExportProcessor` with the four batch-options fields -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:155-162`.
- The `AddOtlpExporter` (non-UseOtlpExporter) path reads
  `OtlpExporterOptions.ExportProcessorType` and
  `OtlpExporterOptions.BatchExportProcessorOptions` instead of
  `ActivityExportProcessorOptions` directly. `ActivityExportProcessorOptions`
  is therefore only the active consumer when using `UseOtlpExporter`.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

Projects:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

The entry in `existing-tests.md` records `ActivityExportProcessorOptions` as
"resolved in `UseOtlpExporterConfigurationTest` theories". The two tests
below are the only ones in the inventory that exercise this class.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` (Theory: `null` / `"testNamedOptions"`) | Binds `TracingOptions:ExportProcessorType=Simple` and `TracingOptions:BatchExportProcessorOptions:ScheduledDelayMilliseconds=1002` from `IConfiguration`; resolves `IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)` and asserts both values | DI `IOptionsMonitor<ActivityExportProcessorOptions>` | Class-level `IDisposable` snapshot/restore + `[Collection("EnvVars")]` |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` (Theory: `null` / `"testNamedOptions"`) | Wires `ConfigureTracingProcessorOptions` delegate setting the same two properties; resolves via the same `VerifyOptionsApplied` helper | DI `IOptionsMonitor<ActivityExportProcessorOptions>` | Class-level `IDisposable` snapshot/restore + `[Collection("EnvVars")]` |

Both tests assert through the shared `VerifyOptionsApplied` private helper
(`UseOtlpExporterExtensionTests.cs:350-382`) which resolves
`IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)` and checks
`ExportProcessorType == Simple` and
`BatchExportProcessorOptions.ScheduledDelayMilliseconds == 1002`.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or "--" for none.

### 2.1 Constructor and factory env-var reads

`ActivityExportProcessorOptions` has no direct env-var reads. Env-var
values reach it through `BatchExportActivityProcessorOptions`, which is
constructed first (and reads `OTEL_BSP_*` env vars) and then passed to the
`ActivityExportProcessorOptions` internal constructor by the
`ProviderBuilderServiceCollectionExtensions` factory.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType` from env var | -- | No env-var binding; property defaults to `ExportProcessorType.Batch` at type level regardless of env vars | n/a (no env-var binding) |
| `BatchExportProcessorOptions` populated from `OTEL_BSP_*` via `BatchExportActivityProcessorOptions` factory | Covered by `BatchExportActivityProcessorOptionsTests` (separate class) not by any `ActivityExportProcessorOptions` test | `BatchExportActivityProcessorOptions.ScheduledDelayMilliseconds` etc. are set from env vars; the resulting object is passed to `ActivityExportProcessorOptions` internal ctor | missing (no test resolves `IOptionsMonitor<ActivityExportProcessorOptions>` and asserts the env-var values flowed through) |

### 2.2 Priority order

The class has two properties. Neither reads env vars directly, so the
priority chain is: programmatic `Configure<T>` > `IConfiguration` binding >
factory default (which inherits `BatchExportActivityProcessorOptions`
defaults).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType`: `Configure<T>` delegate beats `IConfiguration` binding | -- | Not verified | missing |
| `ExportProcessorType`: `IConfiguration` binding applies (from `TracingOptions` section) | `UseOtlpExporterConfigurationTest` (indirectly, `UseOtlpExporter` path only) | `services.Configure<ActivityExportProcessorOptions>(name, configuration.GetSection("TracingOptions"))` runs; value applied | partial (only tested inside the `UseOtlpExporter` pathway; standalone `AddOpenTelemetry().WithTracing()` pathway not tested) |
| `ExportProcessorType`: factory default (`Batch`) when no `Configure<T>` or `IConfiguration` touches the property | -- | Type default `ExportProcessorType.Batch` | missing |
| `BatchExportProcessorOptions`: `Configure<T>` delegate beats `IConfiguration` binding | -- | Not verified | missing |
| `BatchExportProcessorOptions`: `IConfiguration` binding from `TracingOptions` section (`BatchExportProcessorOptions:ScheduledDelayMilliseconds`) | `UseOtlpExporterConfigurationTest` | Applied via `services.Configure<ActivityExportProcessorOptions>` binding | partial (same limitation as above) |
| `BatchExportProcessorOptions`: factory default (from `BatchExportActivityProcessorOptions` env-var read) | -- | `BatchExportActivityProcessorOptions` resolved by `IOptionsMonitor` at factory time; its values become the `ActivityExportProcessorOptions.BatchExportProcessorOptions` default | missing |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType` defaults to `ExportProcessorType.Batch` when no env vars or config set | -- | `ExportProcessorType.Batch` (type-level default on line 37) | missing |
| `BatchExportProcessorOptions` defaults match `BatchExportActivityProcessorOptions` defaults when constructed via factory with no env vars | -- | Factory passes `IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(name)` which itself defaults from `BatchExportActivityProcessorOptions` constructor defaults | missing |
| `BatchExportProcessorOptions` setter null-guard throws `ArgumentNullException` | -- | `Guard.ThrowIfNull` at line 47 | missing |

### 2.4 Named-options

`ActivityExportProcessorOptions` is registered without a fixed options name
by the `AddOpenTelemetryTracerProviderBuilderServices` factory. Named access
is via `IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)` where
`name` is the `UseOtlpExporter` builder name (default `"otlp"`) or the
`AddOtlpExporter` named-options name.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Named access (`Get("testNamedOptions")`) returns distinct instance with `IConfiguration`-applied values | `UseOtlpExporterConfigurationTest` (Theory `"testNamedOptions"` row) | Named options resolved correctly | covered |
| Unnamed access (`Get(null)` / `Get("otlp")`) returns instance with `IConfiguration`-applied values | `UseOtlpExporterConfigurationTest` (Theory `null` row) | Default-name options resolved correctly | covered |
| Named instance is distinct from unnamed instance (no shared mutation) | -- | Not tested for this class specifically | missing |

### 2.5 Invalid-input characterisation

| Property | Malformed input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `ExportProcessorType` | Unknown enum value (e.g. `(ExportProcessorType)99`) set programmatically | Stored as-is; `BuildOtlpExporterProcessor` silently treats unknown value as `Batch` because the branch is `== Simple` (else goes to Batch path) | -- | missing (silent accept) |
| `ExportProcessorType` | Unknown string from `IConfiguration` binding | `IConfiguration` binder fails to parse; property retains factory default (`Batch`) | -- | missing |
| `BatchExportProcessorOptions` | Programmatic `null` assignment | `Guard.ThrowIfNull` throws `ArgumentNullException` | -- | missing |

All invalid-input rows are expected to change under Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart` for all options classes).

### 2.6 Reload no-op baseline

`ActivityExportProcessorOptions` does not participate in reload today. The
factory reads `BatchExportActivityProcessorOptions` once during
`IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)`. A reload of
`IConfigurationRoot` would cause `IOptionsMonitor` to re-run the factory,
but the built `BatchActivityExportProcessor` or `SimpleActivityExportProcessor`
is not reconstructed.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` -> `IOptionsMonitor<ActivityExportProcessorOptions>.OnChange` fires | -- | Not verified | missing |
| `IConfigurationRoot.Reload()` -> built processor type (Simple vs Batch) unchanged | -- | Not verified | missing |
| `IConfigurationRoot.Reload()` -> built `BatchActivityExportProcessor` intervals unchanged | -- | Not verified | missing |

All three reload rows are expected to flip under Issue 20 (export
enable/disable kill-switch via `OnChange` in `BatchExportProcessor`) and
Issue 21 (wire `OnChange` for batch and metric export intervals).

### 2.7 Consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType == Simple` -> `SimpleActivityExportProcessor` built | -- | `BuildOtlpExporterProcessor` line 155-156: `exportProcessorType == ExportProcessorType.Simple ? new SimpleActivityExportProcessor(...)` | missing (no test checks the built processor type via InternalAccessor or Mock) |
| `ExportProcessorType == Batch` -> `BatchActivityExportProcessor` built | -- | `BuildOtlpExporterProcessor` line 157-162: `new BatchActivityExportProcessor(...)` with the batch fields | missing |
| `BatchExportProcessorOptions.ScheduledDelayMilliseconds` -> `BatchActivityExportProcessor.scheduledDelayMilliseconds` | -- | Passed to `BatchActivityExportProcessor` constructor at line 160 | missing |
| `BatchExportProcessorOptions` null fallback in `AddOtlpExporter` path -> `new BatchExportActivityProcessorOptions()` used | -- | `OtlpTraceExporterHelperExtensions.cs:112` has `exporterOptions.BatchExportProcessorOptions ?? new BatchExportActivityProcessorOptions()` | missing |

---

## 3. Recommendations

One recommendation per gap. Test names follow the dominant
`Subject_Condition_Expected` convention from the Session 0a naming survey.
Target locations are the existing test files unless otherwise noted. Tier
and observation mechanism per entry-doc Sections 2 and 3.

### 3.1 Default-state and factory flow

1. **`ActivityExportProcessorOptions_Defaults`** (new test in
   `test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`
   or a new `ActivityExportProcessorOptionsTests.cs` in the same directory).
   - Tier 1. Mechanism: DirectProperty. Constructs an instance with `new
     ActivityExportProcessorOptions()` and asserts
     `ExportProcessorType == ExportProcessorType.Batch` and that
     `BatchExportProcessorOptions` is non-null.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins type-level defaults. No planned
     change expected. Observation: DirectProperty."
   - Risk vs reward: trivial to write; closes the only completely
     uncovered base scenario for this class.
2. **`ActivityExportProcessorOptions_ViaDi_Defaults`** (new test in a
   new `ActivityExportProcessorOptionsTests.cs`).
   - Tier 2. Mechanism: DI (`IOptionsMonitor<ActivityExportProcessorOptions>
     .Get(Options.DefaultName)` after registering
     `AddOpenTelemetryTracerProviderBuilderServices`). Asserts
     `ExportProcessorType == Batch` and that `BatchExportProcessorOptions`
     carries the `BatchExportActivityProcessorOptions` defaults (e.g.
     `ScheduledDelayMilliseconds == 5000`).
   - Guards Issues 1, 21.
   - Code-comment hint: "BASELINE: pins factory-wired defaults observed
     via DI. Observation: DI - exercises
     `ProviderBuilderServiceCollectionExtensions.AddOpenTelemetryTracerProviderBuilderServices`
     factory at line 54-56."
   - Risk vs reward: low effort; higher coverage value than Tier 1 alone
     because it exercises the factory delegation chain.
3. **`ActivityExportProcessorOptions_BatchExportProcessorOptions_NullSetter_ThrowsArgumentNullException`**
   (new test, same file).
   - Tier 1. Mechanism: Exception. Asserts
     `Assert.Throws<ArgumentNullException>(
     () => opts.BatchExportProcessorOptions = null!)`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins null guard at line 47. No planned
     change. Observation: DirectProperty."
   - Risk vs reward: trivial; pins a load-bearing guard.

### 3.2 Factory env-var flow

1. **`ActivityExportProcessorOptions_ViaDi_EnvVarsFlowFromBatchOptions`**
   (new test, same file as 3.1 items).
   - Tier 2. Mechanism: DI + class-level
     `IDisposable` snapshot/restore. Sets `OTEL_BSP_SCHEDULE_DELAY=3000`,
     builds the DI container, resolves
     `IOptionsMonitor<ActivityExportProcessorOptions>.Get(Options.DefaultName)`,
     asserts `BatchExportProcessorOptions.ScheduledDelayMilliseconds == 3000`.
   - Guards Issues 1, 21.
   - Code-comment hint: "BASELINE: pins that env vars for
     `BatchExportActivityProcessorOptions` flow through the factory into
     `ActivityExportProcessorOptions`. Observation: DI - env-var snapshot
     required."
   - Risk vs reward: moderate (requires env-var isolation); high value
     because this is the only path by which env-var configuration reaches
     `ActivityExportProcessorOptions`.

### 3.3 Priority order

1. **`ActivityExportProcessorOptions_ConfigureDelegate_BeatsIConfiguration`**
   (new test in `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI. Registers both
     `UseOtlpExporter(configuration)` (with `TracingOptions:ExportProcessorType=Simple`)
     and a `ConfigureTracingProcessorOptions` delegate that sets
     `ExportProcessorType = ExportProcessorType.Batch`. Resolves
     `IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)` and
     asserts `Batch` wins (Configure delegates run after `IConfiguration`
     binding in the options pipeline).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins `Configure<T>` > `IConfiguration`
     binding order for this class. Observation: DI."
   - Risk vs reward: moderate setup; pins a non-obvious precedence for
     options with dual configuration sources.

### 3.4 Invalid-input characterisation

1. **`ActivityExportProcessorOptions_ExportProcessorType_UnknownEnum_IsAcceptedSilently`**
   (new test in `ActivityExportProcessorOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty + consumer side-effect check
     (if feasible at Tier 1 by calling `BuildOtlpExporterProcessor` with a
     mock exporter). Sets `ExportProcessorType = (ExportProcessorType)99`.
     Asserts the value is stored as-is with no exception today; notes that
     the consumer silently falls through to the Batch branch.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins silent accept of unknown enum.
     Expected to change under Issue 1 (validation). Observation:
     DirectProperty."
   - Risk vs reward: low; establishes the pre-validation baseline.
2. **`ActivityExportProcessorOptions_ExportProcessorType_UnknownString_FromIConfiguration_UsesDefault`**
   (new test, same file).
   - Tier 1. Mechanism: DirectProperty after `IConfiguration` binding
     via `services.Configure<ActivityExportProcessorOptions>()`. Provides
     `ExportProcessorType = "Invalid"` via `AddInMemoryCollection`. Asserts
     property retains `ExportProcessorType.Batch` (IConfiguration binder
     falls back to default on enum parse failure).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins silent IConfiguration fallback.
     Expected to change under Issue 1."
   - Risk vs reward: low effort; closes one silent-failure mode.

### 3.5 Consumer-observed processor type selection

1. **`ActivityExportProcessorOptions_ExportProcessorType_Simple_WiresSimpleProcessor`**
   and **`ActivityExportProcessorOptions_ExportProcessorType_Batch_WiresBatchProcessor`**
   (new tests in `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: InternalAccessor. After building a full
     `AddOpenTelemetry().UseOtlpExporter(configure: ...)` pipeline with
     `ExportProcessorType = Simple` (or `Batch`), cast the resolved
     `TracerProvider` to `TracerProviderSdk` (accessible via
     `InternalsVisibleTo`; already demonstrated by disposal tests in Session
     0a Sec.3.C) and check the processor chain type.
   - Guards Issues 1, 14, 20.
   - Code-comment hint: "BASELINE: pins that `ExportProcessorType` drives
     processor selection in `BuildOtlpExporterProcessor` at line 155.
     Observation: InternalAccessor - uses `TracerProviderSdk` processor
     list."
   - Risk vs reward: moderate (requires InternalAccessor on
     `TracerProviderSdk`); high value because no current test closes the
     loop between the options value and the built processor type.
2. **`ActivityExportProcessorOptions_ScheduledDelay_FlowsToBatchProcessor`**
   (new test, same file).
   - Tier 2. Mechanism: Reflection on
     `BatchExportProcessor<Activity>.scheduledDelayMilliseconds` (private
     field; already identified as a target in the `otlp-exporter-options.md`
     Section 3.5 precedent). Sets `ScheduledDelayMilliseconds = 4000` via
     `ConfigureTracingProcessorOptions`, builds the pipeline, reflects to
     confirm the field. Code-comment must include: "Brittle to internal
     rename of `scheduledDelayMilliseconds`; replace with InternalAccessor
     if one is added."
   - Guards Issues 1, 21.
   - Code-comment hint: "BASELINE: pins that
     `BatchExportProcessorOptions.ScheduledDelayMilliseconds` reaches the
     built processor. Observation: Reflection - field name
     `scheduledDelayMilliseconds` in `BatchExportProcessor<T>`."
   - Risk vs reward: moderate brittleness (reflection on private field);
     high value because no current test closes this flow.

### 3.6 Reload no-op baseline

Shared pathway spec applies; see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

1. **`ActivityExportProcessorOptions_ReloadOfConfiguration_DoesNotChangeBuiltProcessorType`**
   (new test in `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI + InternalAccessor. Builds the pipeline with a
     `IMemoryCollection`-backed `IConfiguration`, triggers
     `IConfigurationRoot.Reload()` with a changed
     `TracingOptions:ExportProcessorType` value, asserts the processor in
     `TracerProviderSdk` is unchanged.
   - Guards Issues 20, 21.
   - Code-comment hint: "BASELINE: pins no-op reload. Expected to flip
     under Issue 20 (kill-switch) and Issue 21 (interval reload).
     Observation: InternalAccessor."
   - Risk vs reward: moderate; the reload no-op suite is the primary safety
     net before Issue 20 and 21 land.

### Prerequisites and dependencies

- 3.1 and 3.2 require a new `ActivityExportProcessorOptionsTests.cs` file
  in `test/OpenTelemetry.Tests/Trace/`. The existing
  `BatchExportActivityProcessorOptionsTests.cs` is a close neighbour and
  can be used as a structural template.
- 3.2 depends on the env-var isolation pattern decision (entry-doc
  Section 5). Class-level `IDisposable` snapshot/restore (already used in
  `BatchExportActivityProcessorOptionsTests`) is the natural choice.
- 3.5 and 3.6 depend on the InternalAccessor pattern for `TracerProviderSdk`
  already demonstrated in `OpenTelemetryServicesExtensionsTests`
  (Session 0a Sec.3.C). No new `InternalsVisibleTo` entry is needed; the
  existing wiring covers the OTLP test project.
- 3.6 depends on the
  [`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md)
  pathway file for the shared reload pattern.

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1 (`_Defaults`,
  `_NullSetter_Throws`), 3.3, 3.4, 3.5.
- **Issue 14** - Register OTLP exporter component factories. Guarded by:
  Section 3.5 (processor type selection at the consumer site).
- **Issue 20** - Export enable/disable kill-switch via `OnChange` in
  `BatchExportProcessor`. Guarded by: Section 3.6.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Sections 3.2, 3.5.2, 3.6.

Reciprocal "Baseline tests required" lines should be added to each of the
issues above citing this file. Those edits happen in the final
cross-reference sweep.
