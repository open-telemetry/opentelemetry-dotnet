# BatchExportActivityProcessorOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Derived type declaration -
  `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:15`.
- Env-var name constants (`MaxQueueSizeEnvVarKey`, `MaxExportBatchSizeEnvVarKey`,
  `ExporterTimeoutEnvVarKey`, `ScheduledDelayEnvVarKey`) -
  `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:17-23`.
- Public parameterless constructor (builds own `IConfiguration` from
  env vars, then calls the internal constructor) -
  `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:28-31`.
- Internal constructor that takes `IConfiguration` and reads all four
  `OTEL_BSP_*` env vars via `TryGetIntValue` -
  `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:33-54`.
- Base class `BatchExportProcessorOptions<Activity>` property declarations
  (all four properties with defaults sourced from `BatchExportProcessor<T>`
  constants) -
  `src/OpenTelemetry/BatchExportProcessorOptions.cs:10-32`.
- Base-class default constants (`DefaultMaxQueueSize` = 2048,
  `DefaultScheduledDelayMilliseconds` = 5000,
  `DefaultExporterTimeoutMilliseconds` = 30000,
  `DefaultMaxExportBatchSize` = 512) -
  `src/OpenTelemetry/BatchExportProcessor.cs:17-20`.
- `BatchExportProcessor<T>` constructor guards (`Guard.ThrowIfOutOfRange`) -
  `src/OpenTelemetry/BatchExportProcessor.cs:46-49`. This is where the
  options values are first validated at the consumer; no validation exists
  on the options class itself today.
- `BatchExportProcessor<T>` internal fields that store the consumed values
  (`ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`,
  `MaxExportBatchSize` - all `internal readonly int`) -
  `src/OpenTelemetry/BatchExportProcessor.cs:22-24`.

### DI registration

`BatchExportActivityProcessorOptions` is registered via
`DelegatingOptionsFactory` inside
`ProviderBuilderServiceCollectionExtensions.AddOpenTelemetryTracerProviderBuilderServices` -
`src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:53`.
The factory signature is
`configuration => new BatchExportActivityProcessorOptions(configuration)`,
which passes the DI-resolved `IConfiguration` to the internal constructor.
`ActivityExportProcessorOptions` is then registered with a factory that
resolves `IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(name)` -
`src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:54-56`.

### Direct consumer sites

- `BatchActivityExportProcessor` constructor (the concrete processor) reads
  all four properties positionally when constructing the worker -
  `src/OpenTelemetry/Trace/Processor/BatchActivityExportProcessor.cs:22-35`.
  The processor constructor delegates to
  `BatchExportProcessor<T>(exporter, maxQueueSize, scheduledDelayMilliseconds,
  exporterTimeoutMilliseconds, maxExportBatchSize)`.
- `OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor` reads the
  four properties from the `batchExportProcessorOptions` parameter and
  passes them positionally to `BatchActivityExportProcessor` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:157-162`.
- `BatchExportProcessor<T>` internal readonly fields (`ScheduledDelayMilliseconds`,
  `ExporterTimeoutMilliseconds`, `MaxExportBatchSize`) are set once at
  construction from the constructor parameters -
  `src/OpenTelemetry/BatchExportProcessor.cs:52-54`. They are observable
  from tests via `InternalsVisibleTo` (the core SDK grants access to
  `OpenTelemetry.Tests`).

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

All six tests live in
`test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`.

Abbreviated project prefix:

- `OT` = `test/OpenTelemetry.Tests/`

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_Defaults` | All four properties at default values when no env vars are set | DirectProperty | Class-level `IDisposable` `ClearEnvVars` in ctor and `Dispose` |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_EnvironmentVariableOverride` | All four `OTEL_BSP_*` env vars override defaults | DirectProperty after ctor | Class-level `IDisposable` |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_UsingIConfiguration` | All four properties set via `AddInMemoryCollection` `IConfiguration` | DirectProperty after internal ctor | Not env-var dependent |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | Non-numeric env var values silently fall back to defaults | DirectProperty | Class-level `IDisposable` |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_SetterOverridesEnvironmentVariable` | Programmatic setter takes precedence over an env var | DirectProperty | Class-level `IDisposable` |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_EnvironmentVariableNames` | Env-var key constants match the `OTEL_BSP_*` spec strings | DirectProperty on constants | Not env-var dependent |

No tests for `BatchExportActivityProcessorOptions` exist in the OTLP or
Hosting test projects for this class directly. `UseOtlpExporterConfigureTest`
and `UseOtlpExporterConfigurationTest` (OTLP project) exercise
`BatchExportActivityProcessorOptions` as a side effect of resolving
`OtlpExporterBuilderOptions`, but they do not assert on the batch-processor
option values and are not counted as coverage here.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

Properties derive from `BatchExportProcessorOptions<Activity>`. All four
env-var reads are in the *derived* constructor
(`BatchExportActivityProcessorOptions(IConfiguration)`); the base class
has no constructor logic.

### 2.1 Constructor env-var reads (per property)

All four `OTEL_BSP_*` reads are in
`BatchExportActivityProcessorOptions(IConfiguration)` at lines 35-53 of
the derived type; none are in the base class.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExporterTimeoutMilliseconds` from `OTEL_BSP_EXPORT_TIMEOUT` | `BatchExportProcessorOptions_EnvironmentVariableOverride` | Parsed `int` via `TryGetIntValue`; default (30000) kept on failure | covered |
| `MaxExportBatchSize` from `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` | `BatchExportProcessorOptions_EnvironmentVariableOverride` | Parsed `int`; default (512) kept on failure | covered |
| `MaxQueueSize` from `OTEL_BSP_MAX_QUEUE_SIZE` | `BatchExportProcessorOptions_EnvironmentVariableOverride` | Parsed `int`; default (2048) kept on failure | covered |
| `ScheduledDelayMilliseconds` from `OTEL_BSP_SCHEDULE_DELAY` | `BatchExportProcessorOptions_EnvironmentVariableOverride` | Parsed `int`; default (5000) kept on failure | covered |
| Env-var constant names match spec (`OTEL_BSP_*`) | `BatchExportProcessorOptions_EnvironmentVariableNames` | Constants defined on derived type (lines 17-23) | covered |
| All four env vars absent -> all four properties at type defaults | `BatchExportProcessorOptions_Defaults` | All four defaults applied by base property initialisers | covered |

### 2.2 Priority order

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic setter beats env var (single property demonstrated) | `BatchExportProcessorOptions_SetterOverridesEnvironmentVariable` | Setter wins because it runs after the constructor; demonstrated for `ExporterTimeoutMilliseconds` only | partial (only one of four properties demonstrated) |
| `IConfiguration` (appsettings-shaped) overrides env var from process | `BatchExportProcessorOptions_UsingIConfiguration` | Internal ctor receives explicit `IConfiguration`; env vars in process are not applied when explicit config is provided | covered (the internal ctor path is separate from the public ctor path) |
| `Configure<BatchExportActivityProcessorOptions>` delegate beats env var (DI pathway) | - | Unverified: the DI factory builds the options with env-var state; Microsoft.Extensions.Options then applies `Configure<T>` delegates after factory creation | missing |
| `Configure<BatchExportActivityProcessorOptions>` beats `appsettings.json` (DI pathway) | - | Unverified | missing |
| Factory default (type default constant) applied when neither env var nor `IConfiguration` touches the property | `BatchExportProcessorOptions_Defaults` | All four property initialisers pick up `BatchExportProcessor<T>.Default*` constants | covered |
| Type defaults observed via DI resolution | - | Not directly tested; `DelegatingOptionsFactory` passes DI `IConfiguration` to the internal ctor; env-var state at DI-build time is the operative source | missing |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All four properties at their defaults (no env vars, no config) | `BatchExportProcessorOptions_Defaults` | `ExporterTimeoutMilliseconds`=30000, `MaxExportBatchSize`=512, `MaxQueueSize`=2048, `ScheduledDelayMilliseconds`=5000 | covered at property level |
| Stable snapshot of the full default shape | - | Not snapshotted | missing (candidate for snapshot-library pilot) |

### 2.4 Named options

`BatchExportActivityProcessorOptions` is not registered with a named
factory in `AddOpenTelemetryTracerProviderBuilderServices`; the
`RegisterOptionsFactory` call at line 53 of
`ProviderBuilderServiceCollectionExtensions.cs` does not include a name
parameter. Microsoft.Extensions.Options therefore returns the same options
instance for all names via `IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(name)`.

**N/A - single instance.** There is no named-options differentiation for
this class today. The `ActivityExportProcessorOptions` factory resolves
`IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(name)` but the
underlying factory always creates with the same env-var-backed `IConfiguration`.
This behaviour should be pinned as a baseline.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<BatchExportActivityProcessorOptions>.Get(Options.DefaultName)` returns env-var-backed defaults | - | Factory at line 53 builds instance from DI `IConfiguration`; all names resolve to equivalent instances | missing |
| Named `Get("foo")` returns an instance with same values as default (single factory, no per-name differentiation) | - | Not pinned; the same instance or equivalent is returned for any name | missing |

### 2.5 Invalid-input characterisation

Each property: what does the code do today when the input is malformed,
out of range, or wrong type? Pins current behaviour so Issue 1 validation
work has a visible delta.

| Property | Input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `ExporterTimeoutMilliseconds` | Env var non-numeric | `TryGetIntValue` returns false; default (30000) kept; logs via `OpenTelemetrySdkEventSource` | `BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | covered |
| `ExporterTimeoutMilliseconds` | Env var zero or negative | Accepted (stored as-is); `BatchExportProcessor<T>` ctor applies `Guard.ThrowIfOutOfRange(min: 0)` so 0 is allowed at the processor level, but negative values throw at construction | - | missing (silent acceptance at options level for negative) |
| `MaxExportBatchSize` | Env var non-numeric | `TryGetIntValue` returns false; default (512) kept | `BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | covered |
| `MaxExportBatchSize` | Env var zero or negative | Accepted at options level; processor ctor rejects with `Guard.ThrowIfOutOfRange(min: 1)` | - | missing (deferred throw at consumer) |
| `MaxExportBatchSize` | Programmatic value > `MaxQueueSize` | Accepted at options level; processor ctor rejects with `Guard.ThrowIfOutOfRange(max: maxQueueSize)` | - | missing (deferred throw at consumer) |
| `MaxQueueSize` | Env var non-numeric | `TryGetIntValue` returns false; default (2048) kept | `BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | covered |
| `MaxQueueSize` | Env var zero or negative | Accepted at options level; processor ctor rejects with `Guard.ThrowIfOutOfRange(min: 1)` | - | missing (deferred throw at consumer) |
| `ScheduledDelayMilliseconds` | Env var non-numeric | `TryGetIntValue` returns false; default (5000) kept | `BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | covered |
| `ScheduledDelayMilliseconds` | Env var zero or negative | Accepted at options level; processor ctor rejects with `Guard.ThrowIfOutOfRange(min: 1)` | - | missing (deferred throw at consumer) |

All missing rows are expected to change under Issue 1 (add
`IValidateOptions<T>` + `ValidateOnStart`). Tests added here pin today's
silent-accept or deferred-throw behaviour so Issue 1 produces a visible
delta.

### 2.6 Reload no-op baseline

Today, `BatchExportActivityProcessorOptions` does not participate in
reload. The DI factory reads env vars once at construction; built
`BatchActivityExportProcessor` instances store the values as `internal
readonly int` fields and never re-consume options. `IOptionsMonitor.OnChange`
subscriptions fire on `IConfiguration` reload but no component acts on them.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<BatchExportActivityProcessorOptions>.OnChange` fires on `IConfigurationRoot.Reload()` | - | Not verified | missing |
| Built `BatchActivityExportProcessor.ScheduledDelayMilliseconds` unchanged after reload | - | Not verified; field is `readonly` | missing |
| Built `BatchActivityExportProcessor.ExporterTimeoutMilliseconds` unchanged after reload | - | Not verified; field is `readonly` | missing |
| `MaxExportBatchSize` unchanged after reload (will remain restart-required per Issue 21) | - | Not verified; field is `readonly` | missing |

The first three rows are expected to flip under Issue 17 (standard `OnChange`
subscriber pattern) and Issue 21 (`ScheduledDelayMilliseconds` and
`ExporterTimeoutMilliseconds` changed from `readonly` to `volatile`;
`MaxExportBatchSize` stays restart-required).

### 2.7 Consumer-observed effects

Behaviours only visible at the consumer (`BatchActivityExportProcessor`
or `BatchExportProcessor<T>`). The consumer fields are `internal readonly int`,
accessible to `OpenTelemetry.Tests` via `InternalsVisibleTo`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `MaxQueueSize` from options flows to `BatchActivityExportProcessor` queue capacity | - | Passed positionally to `BatchExportProcessor<T>` ctor; stored as `CircularBuffer` capacity | missing |
| `ScheduledDelayMilliseconds` from options flows to `BatchExportProcessor<T>.ScheduledDelayMilliseconds` internal field | - | Copied into internal readonly field at `src/OpenTelemetry/BatchExportProcessor.cs:52` | missing |
| `ExporterTimeoutMilliseconds` from options flows to `BatchExportProcessor<T>.ExporterTimeoutMilliseconds` internal field | - | Copied into internal readonly field at `src/OpenTelemetry/BatchExportProcessor.cs:53` | missing |
| `MaxExportBatchSize` from options flows to `BatchExportProcessor<T>.MaxExportBatchSize` internal field | - | Copied into internal readonly field at `src/OpenTelemetry/BatchExportProcessor.cs:54` | missing |
| Guard throws `ArgumentException` when `MaxQueueSize` < 1 at processor construction | - | `Guard.ThrowIfOutOfRange(maxQueueSize, min: 1)` at `src/OpenTelemetry/BatchExportProcessor.cs:46` | missing (no test pins the throw boundary) |
| Guard throws `ArgumentException` when `MaxExportBatchSize` < 1 or > `MaxQueueSize` at processor construction | - | `Guard.ThrowIfOutOfRange(maxExportBatchSize, min: 1, max: maxQueueSize, ...)` at `src/OpenTelemetry/BatchExportProcessor.cs:47` | missing |
| Guard throws `ArgumentException` when `ScheduledDelayMilliseconds` < 1 at processor construction | - | `Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1)` at `src/OpenTelemetry/BatchExportProcessor.cs:48` | missing |
| `ExporterTimeoutMilliseconds` = 0 is accepted by processor ctor (`min: 0` guard) | - | `Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0)` at `src/OpenTelemetry/BatchExportProcessor.cs:49` | missing |

---

## 3. Recommendations

One bullet per gap. Each recommendation targets a reviewable PR unit.
Test names follow the dominant `Subject_Condition_Expected` convention.
Target location is the existing test file for the scenario.

### 3.1 Priority-order and DI-resolved defaults

1. **`BatchExportProcessorOptions_SetterOverridesEnvironmentVariable_AllProperties`**
   (extended or companion Theory in
   `BatchExportActivityProcessorOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. The existing test only covers
     `ExporterTimeoutMilliseconds`. Extend to a `[Theory]` covering all four
     properties or add three companion `[Fact]` methods so every property
     has explicit setter-beats-env-var coverage.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins setter > env var for each property
     individually. No planned change to priority order."
   - Risk vs reward: minimal effort; ensures no silent regression when
     property initialisers or constructor ordering changes.

2. **`BatchExportProcessorOptions_Defaults_ObservedViaDi`** (new test in
   `BatchExportActivityProcessorOptionsTests.cs` or a new DI-level file
   in `test/OpenTelemetry.Tests/Trace/`).
   - Tier 2. Mechanism: DI
     (`IServiceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>()
     .Get(Options.DefaultName)`). Builds a minimal `IServiceCollection` via
     `AddOpenTelemetryTracerProviderBuilderServices`, resolves the monitor,
     and asserts all four defaults.
   - Guards Issues 1, 17. Risks pinned: `2.1`.
   - Code-comment hint: "BASELINE: pins factory-produced defaults via DI.
     Observation: DI - factory at
     `ProviderBuilderServiceCollectionExtensions.AddOpenTelemetryTracerProviderBuilderServices`."
   - Risk vs reward: low brittleness; closes the gap between the direct-ctor
     path and what the DI pipeline hands to the processor builder.

3. **`BatchExportProcessorOptions_ConfigureDelegate_BeatsEnvVar`** (new;
   same file or DI test file).
   - Tier 2. Mechanism: DI + env-var set via class-level
     `IDisposable` snapshot/restore. Calls `services.Configure<BatchExportActivityProcessorOptions>`
     after `AddOpenTelemetryTracerProviderBuilderServices`, sets an env var,
     resolves via `IOptionsMonitor`, and asserts the `Configure<T>` value
     wins.
   - Guards Issues 1, 17.
   - Code-comment hint: "BASELINE: pins `Configure<T>` > env var order.
     Expected to remain true under Issue 17."
   - Risk vs reward: moderate setup for a load-bearing precedence row.

4. **`BatchExportProcessorOptions_NamedGet_ReturnsSingleInstance`** (new;
   DI test file).
   - Tier 2. Mechanism: DI. Calls `IOptionsMonitor.Get("name1")` and
     `IOptionsMonitor.Get("name2")` and asserts that the values of all four
     properties are identical (no per-name differentiation today).
   - Guards Issue 1. Pins the current single-factory behaviour so any
     future per-name wiring produces a test delta.
   - Code-comment hint: "BASELINE: pins single-factory behaviour.
     Expected to remain true; delta would appear if per-signal named
     options are introduced."
   - Risk vs reward: low; closes named-options gap.

### 3.2 Invalid-input characterisation (guards Issue 1)

All tests in this group carry the code comment: "BASELINE: pins current
silent-accept or deferred-throw behaviour. Expected to change under
Issue 1 (`IValidateOptions<T>` + `ValidateOnStart`)."

1. **`BatchExportProcessorOptions_ExporterTimeoutMilliseconds_Negative_IsAcceptedByOptions`**
   (new; `BatchExportActivityProcessorOptionsTests.cs`). Tier 1. Mechanism:
   DirectProperty. Sets `options.ExporterTimeoutMilliseconds = -1` and
   asserts the value is stored (no exception from the options class). Pin
   that validation is deferred to the processor constructor.

2. **`BatchExportProcessorOptions_MaxQueueSize_Zero_IsAcceptedByOptions`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Sets
   `options.MaxQueueSize = 0` and asserts stored.

3. **`BatchExportProcessorOptions_MaxExportBatchSize_Zero_IsAcceptedByOptions`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Sets
   `options.MaxExportBatchSize = 0` and asserts stored.

4. **`BatchExportProcessorOptions_ScheduledDelayMilliseconds_Zero_IsAcceptedByOptions`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Sets
   `options.ScheduledDelayMilliseconds = 0` and asserts stored.

5. **`BatchExportProcessorOptions_MaxExportBatchSize_ExceedsMaxQueueSize_IsAcceptedByOptions`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Sets
   `MaxQueueSize = 10`, `MaxExportBatchSize = 20` and asserts both stored
   without exception. Pins the absence of cross-property validation on the
   options class.

### 3.3 Consumer-observed effects (guards Issues 1 and 21)

1. **`BatchExportProcessorOptions_AllProperties_FlowToBuiltProcessor`**
   (new; `BatchExportActivityProcessorOptionsTests.cs` or a new
   processor-consumer test file).
   - Tier 2. Mechanism: InternalAccessor. Build a `BatchActivityExportProcessor`
     with a `DelegatingExporter<Activity>` (shared helper from
     `test/OpenTelemetry.Tests/Shared/DelegatingExporter.cs`). Assert
     `processor.ScheduledDelayMilliseconds`, `processor.ExporterTimeoutMilliseconds`,
     and `processor.MaxExportBatchSize` match the options values.
     `MaxQueueSize` is observable indirectly via `processor.circularBuffer`
     capacity (private field; if brittle, use a Reflection fallback and
     note it in the code comment).
   - Guards Issues 1, 21.
   - Code-comment hint: "BASELINE: pins options-to-processor copy.
     Observation: InternalAccessor -
     `BatchExportProcessor<T>.ScheduledDelayMilliseconds`,
     `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` are `internal
     readonly int` (src/OpenTelemetry/BatchExportProcessor.cs:22-24).
     Expected to change under Issue 21 when `readonly` is replaced with
     `volatile` for two of the three fields."
   - Risk vs reward: low brittleness (InternalsVisibleTo already wired);
     high value because it is the only test that would catch a silent
     mis-mapping between options and processor.

2. **`BatchExportProcessor_Guard_MaxQueueSizeLessThanOne_Throws`**
   (new; processor-consumer test or `BatchExportActivityProcessorOptionsTests.cs`).
   - Tier 1. Mechanism: Exception. Construct
     `new BatchActivityExportProcessor(exporter, maxQueueSize: 0)` and
     assert `ArgumentException` from `Guard.ThrowIfOutOfRange`. Pins the
     boundary so Issue 1 can add an earlier validation.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins deferred-throw boundary at
     processor construction. Expected to change under Issue 1 when
     validation moves to `IValidateOptions<T>`."

3. **`BatchExportProcessor_Guard_MaxExportBatchSizeExceedsMaxQueueSize_Throws`**
   and **`BatchExportProcessor_Guard_ScheduledDelayLessThanOne_Throws`**
   (new; same location as above). Tier 1. Mechanism: Exception. Each pins
   one `Guard.ThrowIfOutOfRange` boundary. Guards Issue 1.

4. **`BatchExportProcessor_Guard_ExporterTimeoutZero_IsAccepted`**
   (new; same location). Tier 1. Mechanism: no exception. Pins that
   `ExporterTimeoutMilliseconds = 0` is explicitly accepted by the processor
   ctor (`min: 0`). Guards Issue 1 so maintainers decide whether the
   options validator should allow 0 as well.

### 3.4 Reload no-op baseline

Shared pathway spec applies; see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

1. **`BatchExportProcessorOptions_ReloadOfConfiguration_DoesNotChangeBuiltProcessorScheduledDelay`**
   (new; DI test file). Tier 2. Mechanism: InternalAccessor. Build a
   `BatchActivityExportProcessor` via DI, trigger `IConfigurationRoot.Reload()`,
   and assert `processor.ScheduledDelayMilliseconds` is unchanged.
   - Guards Issues 17, 21.
   - Code-comment hint: "BASELINE: pins no-op reload for `ScheduledDelayMilliseconds`.
     Expected to flip under Issue 21 when field becomes `volatile`."

2. **`BatchExportProcessorOptions_ReloadOfConfiguration_DoesNotChangeBuiltProcessorExporterTimeout`**
   (new; same file). Tier 2. Mechanism: InternalAccessor. Same pattern as
   above for `ExporterTimeoutMilliseconds`. Guards Issues 17, 21.
   Code-comment hint: "Expected to flip under Issue 21."

3. **`BatchExportProcessorOptions_ReloadOfConfiguration_MaxExportBatchSizeRemainsRestartRequired`**
   (new; same file). Tier 2. Mechanism: InternalAccessor. Same pattern for
   `MaxExportBatchSize`. Guards Issues 17, 21.
   Code-comment hint: "BASELINE: pins restart-required status for
   `MaxExportBatchSize`. Per Issue 21 this property is intentionally left
   as restart-required (avoids per-item volatile read on ARM)."

4. **`BatchExportProcessorOptions_OnChangeSubscription_FiresOnReload_ButProcessorUnchanged`**
   (new; same file). Tier 2. Mechanism: DI + subscription assertion +
   InternalAccessor. Pins that `IOptionsMonitor<BatchExportActivityProcessorOptions>.OnChange`
   fires on reload while the built processor does not act on it.
   Guards Issue 17.

Risk vs reward for 3.4: moderate effort; high value - without this suite,
Issue 21 has no visible test delta when it lands.

### 3.5 Default-state snapshot (pilot-dependent)

1. **`BatchExportProcessorOptions_Default_Snapshot`** (new;
   `BatchExportActivityProcessorOptionsTests.cs` or a dedicated
   `Snapshots/` subfolder per the snapshot-library choice in entry-doc
   Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD by maintainers).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins whole-options default shape.
     Snapshot update expected on any additive property change; reviewer
     confirms intent."
   - Risk vs reward: low per-test cost once the library is chosen; high
     value for catching silent default drift.

### Prerequisites and dependencies

- 3.1 (DI tests) and 3.4 depend on the env-var isolation pattern decision
  (entry-doc Section 5). The existing class-level `IDisposable` pattern in
  `BatchExportActivityProcessorOptionsTests` can be reused; new DI tests
  that set env vars must participate in the same class-level isolation or
  use `EnvironmentVariableScope`.
- 3.4 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  landing first so the no-op tests can follow a shared template.
- 3.5 depends on the snapshot-library selection
  ([entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.5.
- **Issue 17** - Design and implement standard `OnChange` subscriber
  pattern. Guarded by: Sections 3.1, 3.4.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Sections 3.3, 3.4. Note: `ScheduledDelayMilliseconds` and
  `ExporterTimeoutMilliseconds` are reload candidates; `MaxExportBatchSize`
  is explicitly restart-required per Issue 21.

Reciprocal "Baseline tests required" lines should be added to each of the
issues above, citing this file. Those edits happen in the final
cross-reference sweep, not here.
