# BatchExportLogRecordProcessorOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `BatchExportLogRecordProcessorOptions` type declaration, env-var key
  constants, public parameterless constructor, internal
  `IConfiguration`-accepting constructor -
  `src/OpenTelemetry/Logs/Processor/BatchExportLogRecordProcessorOptions.cs:15-55`.
  - `MaxQueueSizeEnvVarKey` (`"OTEL_BLRP_MAX_QUEUE_SIZE"`) - line 17.
  - `MaxExportBatchSizeEnvVarKey` (`"OTEL_BLRP_MAX_EXPORT_BATCH_SIZE"`) - line 19.
  - `ExporterTimeoutEnvVarKey` (`"OTEL_BLRP_EXPORT_TIMEOUT"`) - line 21.
  - `ScheduledDelayEnvVarKey` (`"OTEL_BLRP_SCHEDULE_DELAY"`) - line 23.
  - Public parameterless constructor (builds its own env-var-backed
    `IConfiguration`) - lines 28-31.
  - Internal `IConfiguration` constructor (reads all four keys via
    `TryGetIntValue`) - lines 33-54.

- `BatchExportProcessorOptions<T>` base class (property declarations and
  default initialisers) -
  `src/OpenTelemetry/BatchExportProcessorOptions.cs:10-32`.
  - `MaxQueueSize` (default `BatchExportProcessor<T>.DefaultMaxQueueSize` =
    2048) - line 16.
  - `ScheduledDelayMilliseconds` (default
    `BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds` = 5000) -
    line 21.
  - `ExporterTimeoutMilliseconds` (default
    `BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds` = 30000) -
    line 26.
  - `MaxExportBatchSize` (default
    `BatchExportProcessor<T>.DefaultMaxExportBatchSize` = 512) - line 31.

- `BatchExportProcessor<T>` default constants and internal observable fields -
  `src/OpenTelemetry/BatchExportProcessor.cs:17-54`.
  - `DefaultMaxQueueSize = 2048` - line 17.
  - `DefaultScheduledDelayMilliseconds = 5000` - line 18.
  - `DefaultExporterTimeoutMilliseconds = 30000` - line 19.
  - `DefaultMaxExportBatchSize = 512` - line 20.
  - `internal readonly int MaxExportBatchSize` - line 22.
  - `internal readonly int ScheduledDelayMilliseconds` - line 23.
  - `internal readonly int ExporterTimeoutMilliseconds` - line 24.
  - `Guard.ThrowIfOutOfRange` calls at construction time guard
    `maxQueueSize >= 1`, `maxExportBatchSize` in `[1, maxQueueSize]`,
    `scheduledDelayMilliseconds >= 1`, `exporterTimeoutMilliseconds >= 0` -
    lines 46-49.

- `BatchLogRecordExportProcessor` (concrete consumer) -
  `src/OpenTelemetry/Logs/Processor/BatchLogRecordExportProcessor.cs:12-69`.
  - Constructor passes the four options values straight through to
    `BatchExportProcessor<T>` base - lines 22-35.

### DI registration and factory

- Options-factory registration for both `BatchExportLogRecordProcessorOptions`
  and `LogRecordExportProcessorOptions` -
  `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:22-26`.
  - Line 23: `services.RegisterOptionsFactory(configuration => new BatchExportLogRecordProcessorOptions(configuration))`.
  - Lines 24-26: `services.RegisterOptionsFactory((sp, configuration, name) => new LogRecordExportProcessorOptions(sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(name)))`.

- `LogRecordExportProcessorOptions` type (wraps
  `BatchExportLogRecordProcessorOptions` and adds `ExportProcessorType`) -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:11-46`.
  - `BatchExportProcessorOptions` property with null-guard setter - lines 37-45.
  - `ExportProcessorType` property (default `Batch`) - line 32.

### Direct consumer

- `OtlpLogExporterHelperExtensions.BuildOtlpLogExporter` reads
  `processorOptions.ExportProcessorType` and `processorOptions.BatchExportProcessorOptions`
  properties and forwards the four batch values as positional arguments to
  `BatchLogRecordExportProcessor` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporterHelperExtensions.cs:275-351`.
  - Batch branch: lines 336-343.

### Named-options note

`BatchExportLogRecordProcessorOptions` has **no named-options semantics of
its own**. It is registered as a single factory; the `LogRecordExportProcessorOptions`
factory calls `.Get(name)` on the underlying monitor, which means the per-name
lifecycle is governed by `LogRecordExportProcessorOptions`, not by
`BatchExportLogRecordProcessorOptions` directly. There is no `UseOtlpExporter`
or `AddOtlpExporter` API that exposes a named `BatchExportLogRecordProcessorOptions`
to callers. Section 2.4 records this as N/A.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

`File:method` abbreviations:

- `OTT` = `test/OpenTelemetry.Tests/`.
- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

### 1.1 `Logs/BatchExportLogRecordProcessorOptionsTests.cs` (OTT)

Five tests. Class is `IDisposable`; constructor and `Dispose` both call
`ClearEnvVars` which nulls all four `OTEL_BLRP_*` env vars. No
`[Collection]` attribute - env-var isolation is class-level only.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_Defaults` | All four properties at default values with no env vars set | DirectProperty | Class-level `IDisposable` snapshot/restore |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | All four `OTEL_BLRP_*` env vars override defaults | DirectProperty after ctor | Class-level `IDisposable` snapshot/restore |
| `BatchExportLogRecordProcessorOptionsTests.ExportLogRecordProcessorOptions_UsingIConfiguration` | All four keys via `AddInMemoryCollection` IConfiguration | DirectProperty after internal ctor | Class-level `IDisposable` snapshot/restore |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable` | Programmatic setter applied after env var wins | DirectProperty | Class-level `IDisposable` snapshot/restore |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_EnvironmentVariableNames` | Verifies `OTEL_BLRP_*` constant string values | DirectProperty (string assert on constants) | Class-level `IDisposable` snapshot/restore |

### 1.2 `OtlpLogExporterTests.cs` (OTPT) - consumer-level test

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | `AddOtlpExporter` wires a `BatchLogRecordExportProcessor` with default `ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` | InternalAccessor (`BatchLogRecordExportProcessor.ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` - internal fields accessible via `InternalsVisibleTo`) | None (no env vars set) |

### 1.3 `UseOtlpExporterExtensionTests.cs` (OTPT) - partial coverage via `UseOtlpExporter`

The two tests below exercise `BatchExportLogRecordProcessorOptions` only as a
side-effect of the wider `UseOtlpExporter` scenario. They are listed because
`existing-tests.md` enumerates them under `BatchExportLogRecordProcessorOptions`
in the `UseOtlpExporterConfigureTest` and `UseOtlpExporterConfigurationTest`
rows; the assertion scope is noted.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | `Configure<LogRecordExportProcessorOptions>` delegate sets `ScheduledDelayMilliseconds = 1000` via the `UseOtlpExporter` builder; resolved via DI | DI `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(name)` | Class-level `IDisposable` + `[Collection("EnvVars")]` |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` | `IConfiguration` section `BatchExportProcessorOptions:ScheduledDelayMilliseconds = 1000` applied to the logging instance | DI `IOptionsMonitor<LogRecordExportProcessorOptions>` | Class-level `IDisposable` + `[Collection("EnvVars")]` |

---

## 2. Scenario checklist and gap analysis

Status values: **covered**, **partial**, **missing**. "Currently tested by"
cites file:method from Section 1, or a dash when none.

### 2.1 Constructor env-var reads (per property, logs-specific)

The `OTEL_BLRP_*` set is the logs-specific env-var group.
`BatchExportLogRecordProcessorOptions` reads exactly these four keys; there is
no inherited base-class env-var read (the base class `BatchExportProcessorOptions<T>`
has no constructor and makes no env-var reads of its own).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExporterTimeoutMilliseconds` from `OTEL_BLRP_EXPORT_TIMEOUT` | `BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | `TryGetIntValue` parses int; if success, overwrites default (30000) | covered |
| `MaxExportBatchSize` from `OTEL_BLRP_MAX_EXPORT_BATCH_SIZE` | `BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | Same pattern; overwrites default (512) | covered |
| `MaxQueueSize` from `OTEL_BLRP_MAX_QUEUE_SIZE` | `BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | Same pattern; overwrites default (2048) | covered |
| `ScheduledDelayMilliseconds` from `OTEL_BLRP_SCHEDULE_DELAY` | `BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | Same pattern; overwrites default (5000) | covered |
| Env-var key constant names match spec (`OTEL_BLRP_*`) | `BatchExportLogRecordProcessorOptions_EnvironmentVariableNames` | Constants are string literals; assertion pins them | covered |
| Absent env var -> type default used (no override) | `BatchExportLogRecordProcessorOptions_Defaults` | `TryGetIntValue` returns false; property stays at base-class initialiser | covered |

### 2.2 `IConfiguration` reads

The internal `IConfiguration` constructor is the DI-registration path (via
`RegisterOptionsFactory`). The public parameterless constructor builds an
env-var-backed `IConfiguration` and delegates to it.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All four keys supplied via `AddInMemoryCollection` IConfiguration | `ExportLogRecordProcessorOptions_UsingIConfiguration` | All four properties set from the provided values | covered |
| Missing key in IConfiguration -> property stays at default | (partially covered by `_Defaults`; that test uses the no-arg ctor with no env vars, not a sparse IConfiguration) | `TryGetIntValue` returns false for absent keys; default kept | partial (no dedicated test for sparse IConfiguration) |
| Type-mismatch value in IConfiguration (non-numeric string) | - | `TryGetIntValue` returns false; default kept; SDK event source may log | missing |

### 2.3 Priority order

Target order for this class: programmatic setter > `IConfiguration` (appsettings
/ `AddInMemoryCollection`) > env var > type default.

The priority emerges from how the env-var-backed ctor and the DI factory chain
together:

1. DI factory creates an instance using the `IConfiguration` injected by the
   host (which includes env vars when using `AddOpenTelemetry` with no custom
   `IConfiguration`).
2. `Configure<BatchExportLogRecordProcessorOptions>` delegates registered by the
   caller run after the factory; they can override any property.
3. The programmatic setter is applied by the `Configure<T>` delegate; it is
   evaluated last and therefore wins.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic setter beats env var (direct construction path) | `BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable` | Object initialiser applied after ctor env-var read; setter value wins | covered |
| `Configure<BatchExportLogRecordProcessorOptions>` delegate beats env var (DI path) | `UseOtlpExporterConfigureTest` (partial; exercises `LogRecordExportProcessorOptions`, not this class directly) | `Configure<T>` runs after factory; delegate value wins | partial (not asserted on `BatchExportLogRecordProcessorOptions` properties directly) |
| `IConfiguration` (appsettings-shaped) beats env var (DI path) | `UseOtlpExporterConfigurationTest` (partial; via `BatchExportProcessorOptions:ScheduledDelayMilliseconds` section key) | IConfiguration-backed factory value wins over default; env var not tested in parallel | partial (no test sets env var and IConfiguration key simultaneously) |
| Factory default applied when neither env var nor IConfiguration touches the property | `BatchExportLogRecordProcessorOptions_Defaults` | Type-default value from `BatchExportProcessor<T>` constants | covered |
| Type defaults observed via DI path (`AddOtlpExporter` without configuration) | `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | `ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` match `BatchLogRecordExportProcessor.Default*` constants | covered (three of four properties; `MaxQueueSize` not asserted in that test) |

### 2.4 Named-options

**N/A - single instance.** `BatchExportLogRecordProcessorOptions` is registered
as a single factory without named-options differentiation. The
`LogRecordExportProcessorOptions` wrapper is resolved per-name, but
`BatchExportLogRecordProcessorOptions` itself is not named. No named-options
test scenarios are needed for this class.

### 2.5 Invalid-input characterisation

Each property has a corresponding `Guard.ThrowIfOutOfRange` check in
`BatchExportProcessor<T>` constructor, not in the options class itself. The
options class silently stores any value; the guard fires only when the options
are consumed to build a `BatchLogRecordExportProcessor`.

| Property | Malformed input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `ExporterTimeoutMilliseconds` | Env var non-numeric string | `TryGetIntValue` returns false; default kept; SDK event source logs | - | missing (no dedicated invalid-env-var test for this class; `BatchExportActivityProcessorOptions` has one, but this class does not) |
| `ExporterTimeoutMilliseconds` | Programmatic negative value | Stored; `Guard.ThrowIfOutOfRange(min: 0)` throws `ArgumentOutOfRangeException` at `BatchLogRecordExportProcessor` construction | - | missing |
| `ExporterTimeoutMilliseconds` | Programmatic zero | Stored; `Guard.ThrowIfOutOfRange(min: 0)` accepts zero | - | missing (current behaviour: zero is accepted at options time; zero timeout at processor build is currently accepted by the guard) |
| `MaxQueueSize` | Programmatic zero or negative | Stored; `Guard.ThrowIfOutOfRange(min: 1)` throws at processor construction | - | missing |
| `MaxExportBatchSize` | Programmatic value exceeding `MaxQueueSize` | Stored; `Guard.ThrowIfOutOfRange(max: maxQueueSize)` throws at processor construction | - | missing |
| `MaxExportBatchSize` | Programmatic zero or negative | Stored; `Guard.ThrowIfOutOfRange(min: 1)` throws at processor construction | - | missing |
| `ScheduledDelayMilliseconds` | Env var non-numeric string | `TryGetIntValue` returns false; default kept | - | missing |
| `ScheduledDelayMilliseconds` | Programmatic zero or negative | Stored; `Guard.ThrowIfOutOfRange(min: 1)` throws at processor construction | - | missing |
| Any property | Env var value out of valid int range (overflow) | `TryGetIntValue` returns false (parse fails); default kept | - | missing |

All rows marked **missing** are expected to change under Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart` for all options classes). Tests
added here pin today's silent-accept / deferred-throw behaviour so Issue 1
produces a visible delta.

Note: unlike `OtlpExporterOptions`, the guards do not live in the options class
itself; they are in `BatchExportProcessor<T>`. Today the options class
accepts arbitrary values silently. Validation under Issue 1 would move the
throw from processor construction to options validation.

### 2.6 Reload no-op baseline

`BatchExportLogRecordProcessorOptions` does not participate in reload today.
The factory reads env vars once at construction. `IOptionsMonitor<BatchExportLogRecordProcessorOptions>.OnChange`
does not trigger any re-construction of a built `BatchLogRecordExportProcessor`.
The `ScheduledDelayMilliseconds` and `ExporterTimeoutMilliseconds` properties
are referenced specifically in Issue 21 as candidates for `OnChange` wiring.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<BatchExportLogRecordProcessorOptions>.OnChange` fires on `IConfigurationRoot.Reload()` | - | Not verified; `OnChange` fires when an `IConfiguration` reload source triggers; no current subscriber acts on the notification | missing |
| Reload of `ScheduledDelayMilliseconds` -> built `BatchLogRecordExportProcessor.ScheduledDelayMilliseconds` unchanged | - | Not verified; processor stores the value as a `readonly` field at construction | missing |
| Reload of `ExporterTimeoutMilliseconds` -> built `BatchLogRecordExportProcessor.ExporterTimeoutMilliseconds` unchanged | - | Not verified; same reason | missing |
| Reload of `MaxQueueSize` and `MaxExportBatchSize` -> processor unchanged | - | Not verified | missing |

All four rows are expected to flip under Issue 17 (standard `OnChange`
subscriber pattern) and Issue 21 (`OnChange` for batch and metric export
intervals). The `ScheduledDelayMilliseconds` and `ExporterTimeoutMilliseconds`
rows are the primary targets of Issue 21.

### 2.7 Consumer-observed effects

The consumer (`BuildOtlpLogExporter`) forwards the four property values
positionally to `BatchLogRecordExportProcessor`. The three `internal readonly`
fields on `BatchExportProcessor<T>` are accessible via `InternalsVisibleTo`
(see existing-tests.md Sec.4.G).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Default `ScheduledDelayMilliseconds` flows to built `BatchLogRecordExportProcessor.ScheduledDelayMilliseconds` | `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | Internal field matches default constant | covered |
| Default `ExporterTimeoutMilliseconds` flows to built processor | `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | Internal field matches default constant | covered |
| Default `MaxExportBatchSize` flows to built processor | `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | Internal field matches default constant | covered |
| Default `MaxQueueSize` flows to built processor (no direct assertion) | - | `CircularBuffer` is sized from `maxQueueSize`; the buffer is a private field, not an internal | missing |
| Non-default `ScheduledDelayMilliseconds` (from env var or Configure) flows to built processor | - | Not verified end-to-end; `_SetterOverridesEnvironmentVariable` asserts on the options object, not the built processor | missing |
| Non-default `ExporterTimeoutMilliseconds` flows to built processor | - | Not verified end-to-end | missing |

---

## 3. Recommendations

One recommendation per gap. Each targets a single reviewable PR unit.
Test names follow the dominant `Subject_Condition_Expected` convention
(entry-doc Section 6). Target file is the existing test file for the
scenario. Tier and observation mechanism per entry-doc Sections 3 and 2.

### 3.1 Invalid-input characterisation (guards Issue 1)

These tests pin today's deferred-throw or silent-fallback behaviour. All carry
the code-comment note: "Expected to change under Issue 1 (`IValidateOptions<T>`
for reload protection; deferred; no `ValidateOnStart`)."

1. **`BatchExportLogRecordProcessorOptions_InvalidEnvironmentVariableOverride`**
   (new test in
   `test/OpenTelemetry.Tests/Logs/BatchExportLogRecordProcessorOptionsTests.cs`).
   - Scenario: set each of the four `OTEL_BLRP_*` env vars to a non-numeric
     string (e.g. `"not-an-int"`), construct the options, assert all four
     properties remain at their defaults.
   - Tier 1. Mechanism: DirectProperty.
   - Guards Issue 1. Risks pinned: none specific.
   - Code-comment hint: "BASELINE: pins that invalid env var silently falls
     back to default. Expected to change under Issue 1 (validation)."
   - Risk vs reward: low effort; directly parallels the
     `BatchExportActivityProcessorOptions` test of the same name; closes a
     clear parity gap between the Activity and LogRecord variants.

2. **`BatchExportLogRecordProcessorOptions_NegativeExporterTimeoutMilliseconds_ThrowsAtProcessorConstruction`**
   (new test in `BatchExportLogRecordProcessorOptionsTests.cs`).
   - Scenario: set `ExporterTimeoutMilliseconds` to `-1` on a constructed options
     object; build a `BatchLogRecordExportProcessor` from it (passing a
     `DelegatingExporter`); assert `ArgumentOutOfRangeException` is thrown.
   - Tier 1. Mechanism: Exception.
   - Guards Issue 1. Note: today the throw is at processor construction, not at
     options set time; this test pins the deferred throw so Issue 1's move to
     options-time validation is visible.
   - Risk vs reward: low effort; high value for pinning today's
     "silent accept + deferred throw" boundary.

3. **`BatchExportLogRecordProcessorOptions_ZeroMaxQueueSize_ThrowsAtProcessorConstruction`**
   (new; same file). Tier 1. Same pattern as above for `MaxQueueSize = 0`.
   Guards Issue 1.

4. **`BatchExportLogRecordProcessorOptions_MaxExportBatchSizeExceedsMaxQueueSize_ThrowsAtProcessorConstruction`**
   (new; same file). Tier 1. Set `MaxExportBatchSize > MaxQueueSize`; assert
   `ArgumentOutOfRangeException` at processor construction. Guards Issue 1.

5. **`BatchExportLogRecordProcessorOptions_ZeroScheduledDelayMilliseconds_ThrowsAtProcessorConstruction`**
   (new; same file). Tier 1. Set `ScheduledDelayMilliseconds = 0`; assert
   `ArgumentOutOfRangeException` at processor construction. Guards Issue 1.

All five share the code-comment template:

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #1 (IValidateOptions<T> for reload protection; deferred; no ValidateOnStart).
// Guards risks: 4.7 (silent failures).
// Observation: Exception - today the guard fires at BatchExportProcessor<T>
//   construction, not at options set time.
// Coverage index: batch-export-logrecord-processor-options.<property>.invalid-input
```

### 3.2 Priority-order coverage (guards Issue 17)

1. **`BatchExportLogRecordProcessorOptions_ConfigureDelegate_BeatsEnvVar`**
   (new test in `BatchExportLogRecordProcessorOptionsTests.cs`).
   - Scenario: register `AddOpenTelemetry` with a `Configure<BatchExportLogRecordProcessorOptions>`
     delegate that sets `ScheduledDelayMilliseconds = 9999`; also set
     `OTEL_BLRP_SCHEDULE_DELAY = 1111` via env var; resolve
     `IOptionsMonitor<BatchExportLogRecordProcessorOptions>` from DI; assert
     `ScheduledDelayMilliseconds == 9999`.
   - Tier 2. Mechanism: DI (`IOptionsMonitor<BatchExportLogRecordProcessorOptions>`).
   - Guards Issues 1, 17. Risks pinned: [Risk 1.1](../../configuration-analysis-risks.md#11-options-validation-is-completely-absent).
   - Code-comment hint: "BASELINE: pins `Configure<T>` > env var order for
     this class. Expected to remain true under Issue 17 (reload), which adds
     a subscriber but does not change the priority rule."
   - Risk vs reward: moderate setup (DI + env-var fixture); high value because
     it closes the priority-order gap that exists for the Activity variant too.

2. **`BatchExportLogRecordProcessorOptions_IConfiguration_BeatsEnvVar`**
   (new; same file).
   - Scenario: build with `services.AddSingleton<IConfiguration>` that
     provides `OTEL_BLRP_SCHEDULE_DELAY = 2222`; also set the env var to
     `1111`; resolve via DI and assert `ScheduledDelayMilliseconds == 2222`.
   - Tier 2. Mechanism: DI.
   - Guards Issue 1, 17. Same code-comment template.
   - Risk vs reward: moderate; closes the most important non-obvious
     precedence gap.

### 3.3 Reload no-op baseline (guards Issues 17 and 21)

Shared pathway spec will live in
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).
The tests below are the `BatchExportLogRecordProcessorOptions`-specific
instances of that pattern.

1. **`BatchExportLogRecordProcessorOptions_ReloadOfConfiguration_DoesNotChangeBuiltProcessorScheduledDelay`**
   (new test in `BatchExportLogRecordProcessorOptionsTests.cs` or a new
   `Logs/BatchExportLogRecordProcessorOptionsReloadTests.cs`).
   - Scenario: build a `BatchLogRecordExportProcessor` via `AddOtlpExporter`
     with `ScheduledDelayMilliseconds = 5000`; trigger
     `IConfigurationRoot.Reload()`; assert
     `BatchLogRecordExportProcessor.ScheduledDelayMilliseconds == 5000`.
   - Tier 2. Mechanism: InternalAccessor
     (`BatchExportProcessor<T>.ScheduledDelayMilliseconds` is `internal readonly`).
   - Guards Issues 17, 21. Risks pinned: none additional.
   - Code-comment hint: "BASELINE: pins no-op reload for batch interval.
     Expected to flip under Issue 21 (OnChange for batch intervals)."
   - Risk vs reward: moderate setup; high value - Issue 21 specifically targets
     this property; without this test there is no visible delta when Issue 21
     lands.

2. **`BatchExportLogRecordProcessorOptions_ReloadOfConfiguration_DoesNotChangeBuiltProcessorExporterTimeout`**
   (new; companion to the above). Same structure, asserts
   `ExporterTimeoutMilliseconds`. Guards Issues 17, 21. Code-comment: same
   template with `ExporterTimeoutMilliseconds`.

3. **`BatchExportLogRecordProcessorOptions_OnChangeSubscription_FiresOnReload`**
   (new; same location).
   - Scenario: subscribe to
     `IOptionsMonitor<BatchExportLogRecordProcessorOptions>.OnChange`; trigger
     `IConfigurationRoot.Reload()`; assert the subscription fires while the
     built processor fields are unchanged.
   - Tier 2. Mechanism: DI + InternalAccessor.
   - Guards Issue 17. Pins that the notification mechanism exists today even
     though no subscriber acts on it.
   - Code-comment hint: "BASELINE: pins that `OnChange` fires on reload.
     Expected to be consumed by Issue 17 subscriber when it lands."
   - Risk vs reward: moderate; high value as a forward-looking baseline.

### 3.4 Consumer-effect gaps (guards Issue 1)

1. **`BatchExportLogRecordProcessorOptions_NonDefaultValues_FlowToBuiltProcessor`**
   (new test in `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpLogExporterTests.cs`).
   - Scenario: register `AddOtlpExporter` with a
     `Configure<BatchExportLogRecordProcessorOptions>` delegate setting all
     four non-default values; build the provider; cast to `LoggerProviderSdk`
     and access the processor; assert all four internal fields match the
     configured values.
   - Tier 2. Mechanism: InternalAccessor (the three internal fields
     `ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`,
     `MaxExportBatchSize` are accessible via `InternalsVisibleTo`; `MaxQueueSize`
     requires verifying via `DroppedCount` after over-filling or via the
     `CircularBuffer` private field at higher brittleness cost - see note).
   - Guards Issue 1. Risks pinned: see configuration-analysis-risks.md (reflection/internal-accessor brittleness).
   - Note: `MaxQueueSize` is not stored as an internal field on
     `BatchExportProcessor<T>`; it is passed to `CircularBuffer<T>` whose
     `Capacity` may be accessible. If `Capacity` is not exposed internally,
     the recommendation is to assert only the three accessible fields and
     document `MaxQueueSize` as a behavioural-observation scenario (Tier 3,
     using a mock exporter that counts dropped items to infer queue depth).
   - Code-comment hint: "BASELINE: pins end-to-end flow of batch options to
     built processor. Guards Issue 1 (validation) and Issue 21 (reload)."
   - Risk vs reward: moderate brittleness on the three internal fields; low
     for `MaxExportBatchSize` and the two time fields; high value for closing
     the options-to-consumer gap.

2. **`BatchExportLogRecordProcessorOptions_MaxQueueSize_NonDefault_DropsExcessItems`**
   (new; optional, Tier 2 or Tier 3 depending on the process-isolation
   decision).
   - Scenario: set `MaxQueueSize = 4`; enqueue five log records synchronously
     via a mock exporter that never drains; assert `DroppedCount >= 1`.
   - Mechanism: InternalAccessor (`BatchExportProcessor<T>.DroppedCount`).
   - Guards Issue 1.
   - Risk vs reward: medium effort; the behavioural approach avoids private-field
     reflection and produces a stable test; lower priority than the previous item.

### 3.5 Sparse IConfiguration gap

1. **`BatchExportLogRecordProcessorOptions_SparseIConfiguration_UseDefaultsForMissingKeys`**
   (new test in `BatchExportLogRecordProcessorOptionsTests.cs`).
   - Scenario: construct the options using the internal ctor with an
     `AddInMemoryCollection` IConfiguration that contains only one key;
     assert the missing three properties remain at defaults.
   - Tier 1. Mechanism: DirectProperty.
   - Guards Issue 1.
   - Risk vs reward: low effort; explicitly pins what is currently implicit from
     the `_Defaults` test.

### Prerequisites and dependencies

- 3.1 tests (recommendations 1-5) are standalone; they depend only on
  `BatchExportLogRecordProcessorOptions` and a test `DelegatingExporter`.
- 3.2 tests depend on the env-var isolation pattern decision (entry-doc
  Section 5); the `IDisposable` class-level pattern already present in
  `BatchExportLogRecordProcessorOptionsTests` is reusable if those tests
  move into the same class, or a `using (new EnvironmentVariableScope(...))`
  block is used.
- 3.3 (reload) tests depend on the reload pathway file
  (`../pathways/reload-no-op-baseline.md`) landing first so the pattern is
  specified once.
- 3.4 tests live in the OTLP test project and depend on `InternalsVisibleTo`
  already wired (entry-doc Sec.4.G confirms it is).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1 (invalid-input characterisation),
  3.2 (priority-order), 3.4 (consumer-effect), 3.5 (sparse IConfiguration).
- **Issue 17** - Design and implement standard `OnChange` subscriber pattern.
  Guarded by: Sections 3.2 (Configure delegate priority), 3.3 (reload no-op
  baseline).
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Section 3.3 (reload no-op baseline for `ScheduledDelayMilliseconds`
  and `ExporterTimeoutMilliseconds`).

Reciprocal "Baseline tests required" lines should be added to each of the
issues above in `configuration-proposed-issues.md`, citing this file. Those
edits happen in the final cross-reference sweep.
