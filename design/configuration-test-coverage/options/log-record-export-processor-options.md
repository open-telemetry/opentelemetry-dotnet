# LogRecordExportProcessorOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:11`.
- Public parameterless constructor (delegates to internal constructor with
  `new BatchExportLogRecordProcessorOptions()`) -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:18-21`.
- Internal constructor (receives a
  `BatchExportLogRecordProcessorOptions` instance, used by the DI factory) -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:23-27`.
- `ExportProcessorType` property (default `ExportProcessorType.Batch`) -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:32`.
- `BatchExportProcessorOptions` property (guarded setter; throws
  `ArgumentNullException` on null) -
  `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:37-45`.
- No env-var reads. The class has no constructor that reads `OTEL_*`
  variables directly; its `BatchExportProcessorOptions` sub-object is
  supplied by the `BatchExportLogRecordProcessorOptions` factory, which
  does read `OTEL_BLRP_*` env vars.

### DI factory registration

- `ProviderBuilderServiceCollectionExtensions.AddOpenTelemetryLoggerProviderBuilderServices` -
  `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:24-26`.
  The factory calls `new LogRecordExportProcessorOptions(sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(name))`,
  wiring the `BatchExportLogRecordProcessorOptions` for the same named
  options slot into the inner field.

### Direct consumer sites

Consumers that read `LogRecordExportProcessorOptions` properties determine
which behaviours are only observable at the consumer.

- `OtlpLogExporterHelperExtensions.BuildOtlpLogExporter` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporterHelperExtensions.cs:275-351`.
  Reads `ExportProcessorType` (line 330) to select
  `SimpleLogRecordExportProcessor` vs `BatchLogRecordExportProcessor`, then
  reads `BatchExportProcessorOptions.*` (lines 336-343) for
  `MaxQueueSize`, `ScheduledDelayMilliseconds`,
  `ExporterTimeoutMilliseconds`, and `MaxExportBatchSize` when the batch
  path is taken.
- `OtlpLogExporterHelperExtensions.AddOtlpExporter` (DI builder pathway) -
  resolves via `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(finalOptionsName)`
  at lines 64 and 112 (two overloads) and line 211 (UseOtlpExporter path);
  the resolved instance is passed directly to `BuildOtlpLogExporter`.
- `OtlpExporterBuilder` (UseOtlpExporter pathway) resolves via
  `IOptionsMonitor<LogRecordExportProcessorOptions>?.Get(name)` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:189`.
  The nullable `.GetService<>()` call is intentional: the options only
  exist when logging is enabled (comment at lines 183-188).
- `OtlpExporterBuilderOptions` - holds the resolved instance as field
  `LogRecordExportProcessorOptions?` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:17`.
  The null case throws at pipeline build time with an
  `InvalidOperationException` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:201`.

### IConfiguration binding

- `OtlpExporterBuilder` binds `LogRecordExportProcessorOptions` via
  `services.Configure<LogRecordExportProcessorOptions>(name, configuration.GetSection("LoggingOptions"))` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:155-156`.
  This is the only path by which an `IConfiguration` section drives this
  class; `AddOtlpExporter` does not bind `IConfiguration` into it
  directly.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

`File:method` is abbreviated to test-method name where the file is
unambiguous. Projects:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | Default `ExportProcessorType.Batch` wired; `BatchLogRecordExportProcessor` with default timings | InternalAccessor (`LoggerProviderSdk.Processor` cast) + public field on processor | Not env-var dependent |
| `OtlpLogExporterTests.AddOtlpLogExporterLogRecordProcessorOptionsTest` | `ExportProcessorType.Simple` and `.Batch` both wired correctly; custom `ScheduledDelayMilliseconds` respected (Theory, x4 inline rows) | InternalAccessor (`LoggerProviderSdk.Processor` cast); for Batch, `BatchLogRecordExportProcessor.ScheduledDelayMilliseconds` | Not env-var dependent |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | `Configure<T>` delegate modifies `LogRecordExportProcessorOptions` for named + unnamed builder (Theory) | DI `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(name)` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` | `UseOtlpExporter(IConfiguration)` binds `LoggingOptions` section into `LogRecordExportProcessorOptions` for named + unnamed (Theory) | DI `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(name)` | `[Collection]` attribute |

Notes:

- No direct tests exist for `LogRecordExportProcessorOptions` in
  `test/OpenTelemetry.Tests/`. The four rows above are all in `OTPT`.
- `UseOtlpExporterConfigureTest` and `UseOtlpExporterConfigurationTest`
  exercise this class indirectly: they resolve
  `IOptionsMonitor<LogRecordExportProcessorOptions>` to confirm that the
  options are present and carry the expected values, but the primary
  assertion focus is on `OtlpExporterBuilderOptions`. The
  `LogRecordExportProcessorOptions`-specific assertions within those
  theories are limited to confirming the binding of the section; they do
  not pin default-state or invalid-input behaviour.
- No test exercises the `BatchExportProcessorOptions` null-setter guard
  directly on this class (the inner guard is on
  `BatchExportLogRecordProcessorOptions.BatchExportProcessorOptions`).
- No named-options subsection applies: this class has no named-options
  semantics of its own beyond what the DI factory supplies per slot.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Constructor and env-var reads

`LogRecordExportProcessorOptions` has no direct env-var reads. The env-var
surface lives entirely in `BatchExportLogRecordProcessorOptions`, which is
covered by its own file
([`batch-export-logrecord-processor-options.md`](batch-export-logrecord-processor-options.md)).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Public ctor produces default `ExportProcessorType.Batch` | `AddOtlpExporterSetsDefaultBatchExportProcessor` (via DI, processor type inferred from cast) | `ExportProcessorType.Batch` | covered |
| Public ctor `BatchExportProcessorOptions` is a new default instance | `AddOtlpExporterSetsDefaultBatchExportProcessor` (asserts default timings match `BatchLogRecordExportProcessor` constants) | New `BatchExportLogRecordProcessorOptions()` | covered |
| Internal ctor receives a `BatchExportLogRecordProcessorOptions` from the DI factory (name-matched slot) | `UseOtlpExporterConfigureTest`, `UseOtlpExporterConfigurationTest` (Theory; DI path) | Named-slot `BatchExportLogRecordProcessorOptions` wired in | partial (covered indirectly; no test asserts the internal ctor is the one called or that a specific named instance is threaded through) |

### 2.2 Priority order

`LogRecordExportProcessorOptions` does not read env vars itself, so the
priority ordering of env var vs `IConfiguration` vs `Configure<T>` applies
only to the `BatchExportProcessorOptions` sub-object (env vars read in the
sub-class constructor) and to `ExportProcessorType` (no env-var source;
only set programmatically or via `IConfiguration` binding).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic `Configure<T>` sets `ExportProcessorType` | `UseOtlpExporterConfigureTest` (Theory; sets and reads via DI) | `Configure<T>` applied by `Microsoft.Extensions.Options` after factory | covered |
| `IConfiguration` section binding sets `ExportProcessorType` | `UseOtlpExporterConfigurationTest` (Theory; `IConfiguration` path via `UseOtlpExporter`) | `services.Configure<LogRecordExportProcessorOptions>(name, section)` | covered |
| Programmatic `Configure<T>` beats `IConfiguration` section for `ExportProcessorType` | - | Unverified; standard `Microsoft.Extensions.Options` pipeline applies (`Configure` before section binding order is registration-order-dependent) | missing |
| `Configure<T>` sets `BatchExportProcessorOptions` reference | `AddOtlpLogExporterLogRecordProcessorOptionsTest` (inline delegate sets both `ExportProcessorType` and a new `BatchExportLogRecordProcessorOptions`) | Delegate mutates the DI-resolved instance directly | covered (at the programmatic-delegate level) |
| `IConfiguration` binding sets `BatchExportProcessorOptions` sub-properties via `LoggingOptions` section | `UseOtlpExporterConfigurationTest` (Theory) | Section bound into `LogRecordExportProcessorOptions`; sub-properties from section | partial (theory asserts some bound values; full per-property coverage is in `batch-export-logrecord-processor-options.md`) |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType` default is `Batch` | `AddOtlpExporterSetsDefaultBatchExportProcessor` | `ExportProcessorType.Batch` | covered |
| `BatchExportProcessorOptions` default produces `BatchLogRecordExportProcessor` with SDK-constant timings | `AddOtlpExporterSetsDefaultBatchExportProcessor` (asserts `DefaultScheduledDelayMilliseconds`, `DefaultExporterTimeoutMilliseconds`, `DefaultMaxExportBatchSize`) | Matches `BatchLogRecordExportProcessor` default constants | covered |
| Stable snapshot of all properties at default (including `ExportProcessorType` and the full `BatchExportProcessorOptions` object graph) | - | Not snapshotted | missing (candidate for snapshot-library pilot after the library is chosen per entry-doc Appendix A) |

### 2.4 Named options

`LogRecordExportProcessorOptions` is registered via
`RegisterOptionsFactory` and therefore participates in named-options
resolution in the same way as other SDK options classes. There is no
named-options behaviour specific to this class beyond the DI factory's
name-threading.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Named-slot `LogRecordExportProcessorOptions` resolved from DI for same name as `AddOtlpExporter` | `AddOtlpLogExporterLogRecordProcessorOptionsTest` (unnamed slot), `UseOtlpExporterConfigureTest` (named + unnamed Theory) | Factory resolves the correct `BatchExportLogRecordProcessorOptions` for the slot name | partial (covered for the happy path; no test pins that a different named slot returns a distinct instance with distinct `BatchExportLogRecordProcessorOptions` state) |
| `Options.DefaultName` vs a custom name produces distinct instances | - | Expected: distinct cached instances, each with their own `BatchExportLogRecordProcessorOptions` | missing |

### 2.5 Invalid-input characterisation

Pin today's behaviour for each property so Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart`) produces a visible delta.

| Property | Malformed input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `ExportProcessorType` | Unknown enum value (e.g. `(ExportProcessorType)99`) | Stored as-is; `BuildOtlpLogExporter` falls through the `if`/`else` into the `Batch` branch because the `if` only tests `Simple`; a non-Simple, non-Batch value silently creates a `BatchLogRecordExportProcessor` | - | missing (silent accept) |
| `BatchExportProcessorOptions` | `null` assigned programmatically | Setter calls `Guard.ThrowIfNull`; throws `ArgumentNullException` | - | missing (throw path not directly tested for this class; tested indirectly via the sub-class) |
| `BatchExportProcessorOptions` | Valid non-null instance with out-of-range sub-properties | Stored; out-of-range sub-properties propagate silently to `BatchLogRecordExportProcessor` constructor | - | missing (covered in `batch-export-logrecord-processor-options.md` at sub-class level; not pinned end-to-end through this class) |

All missing rows are expected to change under Issue 1.

### 2.6 Reload no-op baseline

`LogRecordExportProcessorOptions` does not participate in reload; processors
are built once at provider startup and hold the resolved property values
directly (no ongoing `IOptionsMonitor<T>` subscription in the built
component).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` after `AddOtlpExporter` -> built processor type unchanged | - | Not verified; processor is selected at build time; no subscription wired | missing |
| `IConfigurationRoot.Reload()` after `AddOtlpExporter` -> built `BatchLogRecordExportProcessor` timings unchanged | - | Not verified | missing |
| `IOptionsMonitor<LogRecordExportProcessorOptions>.OnChange` fires on reload but built processor is unaffected | - | Not verified | missing |

All three rows are expected to flip under Issue 20 (export enable/disable
kill-switch in `BatchExportProcessor`) and Issue 21 (wire `OnChange` for
batch intervals). The reload-no-op pattern is specified in the shared
pathway file
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

### 2.7 Consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportProcessorType.Batch` -> `BatchLogRecordExportProcessor` wired | `AddOtlpExporterSetsDefaultBatchExportProcessor`, `AddOtlpLogExporterLogRecordProcessorOptionsTest` | `BuildOtlpLogExporter` creates `BatchLogRecordExportProcessor` | covered |
| `ExportProcessorType.Simple` -> `SimpleLogRecordExportProcessor` wired | `AddOtlpLogExporterLogRecordProcessorOptionsTest` (Theory row with `Simple`) | `BuildOtlpLogExporter` creates `SimpleLogRecordExportProcessor` | covered |
| Custom `BatchExportProcessorOptions.ScheduledDelayMilliseconds` reaches `BatchLogRecordExportProcessor` | `AddOtlpLogExporterLogRecordProcessorOptionsTest` (asserts `batchProcesor.ScheduledDelayMilliseconds == 1000`) | Value propagates via `batchOptions.ScheduledDelayMilliseconds` at line 341 | covered |
| Custom `BatchExportProcessorOptions.MaxQueueSize` reaches `BatchLogRecordExportProcessor` | - | Propagated at line 340; not asserted in any existing test | missing |
| Custom `BatchExportProcessorOptions.ExporterTimeoutMilliseconds` reaches `BatchLogRecordExportProcessor` | - | Propagated at line 342; not asserted in any existing test | missing |
| Custom `BatchExportProcessorOptions.MaxExportBatchSize` reaches `BatchLogRecordExportProcessor` | - | Propagated at line 343; not asserted in any existing test | missing |
| `UseOtlpExporter` with logging enabled and `LogRecordExportProcessorOptions` absent -> `InvalidOperationException` | - | `OtlpExporterBuilder.cs:201` throws when `LogRecordExportProcessorOptions` is null | missing (defensive throw path not pinned) |

---

## 3. Recommendations

One recommendation per gap. Each targets a reviewable PR unit. Test names
follow the dominant `Subject_Condition_Expected` convention (Section 6 of
the entry doc). Target location is the existing test file for the scenario.
Tier mapping per entry-doc Section 3. Observation-mechanism labels match
entry-doc Section 2.

Grouped by theme; within each theme ordered lowest to highest brittleness.

### 3.1 Default-state and constructor coverage

1. **`LogRecordExportProcessorOptions_Defaults_ExportProcessorTypeIsBatch`**
   (new; `OtlpLogExporterTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs
     `new LogRecordExportProcessorOptions()` and asserts
     `ExportProcessorType == ExportProcessorType.Batch` and
     `BatchExportProcessorOptions != null`.
   - No env-var involvement; no isolation machinery required.
   - Guards Issue 1.
   - Code-comment hint:
     "BASELINE: pins current behaviour. No planned change. Observation:
     DirectProperty - public ctor defaults, no DI. Coverage index:
     log-record-export-processor-options.ctor.default"
   - Risk vs reward: very low effort; closes the only missing
     direct-property baseline for a public class. Low brittleness.

2. **`LogRecordExportProcessorOptions_Default_Snapshot`** (new;
   `OtlpLogExporterTests.cs` or a dedicated `Snapshots/` subfolder per
   the snapshot-library choice in entry-doc Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD by maintainers). Pins
     the whole-options shape including the nested
     `BatchExportProcessorOptions` defaults.
   - Guards Issues 1, 20, 21.
   - Code-comment hint: "BASELINE: pins whole-options shape. Snapshot
     update expected on any additive change; reviewer confirms intent.
     Coverage index:
     log-record-export-processor-options.all-properties.default"
   - Risk vs reward: low per-test cost once the library is chosen; high
     value for catching silent default drift across both properties and
     the nested sub-object. Depends on snapshot-library decision
     (entry-doc Appendix A).

### 3.2 Priority-order gaps

3. **`LogRecordExportProcessorOptions_ConfigureDelegate_BeatsIConfiguration`**
   (new; `OtlpLogExporterTests.cs`).
   - Tier 2. Mechanism: DI. Build a `ServiceCollection` with
     `UseOtlpExporter`, provide an `IConfiguration` that sets
     `ExportProcessorType = Simple` in the `LoggingOptions` section, and
     also register a `Configure<LogRecordExportProcessorOptions>` delegate
     that sets `ExportProcessorType = Batch`. Resolve and assert `Batch`
     wins. If `Configure<T>` is registered after the section binding,
     `Microsoft.Extensions.Options` applies it last; this test pins the
     actual registration order to lock in the current behaviour.
   - Guards Issue 1. Risks pinned: `2.1`.
   - Code-comment hint: "BASELINE: pins Configure<T> > IConfiguration
     section order. Expected to remain true; test documents the
     registration ordering dependency. Coverage index:
     log-record-export-processor-options.export-processor-type.configure-beats-iconfiguration"
   - Risk vs reward: moderate setup; high value because the ordering
     is implicit and registration-order-dependent.

4. **`LogRecordExportProcessorOptions_NamedOptions_DistinctSlotsHaveDistinctInstances`**
   (new; `OtlpLogExporterTests.cs`).
   - Tier 2. Mechanism: DI (`IOptionsMonitor<LogRecordExportProcessorOptions>`
     `.Get("slotA")` vs `.Get("slotB")`). Verifies that distinct named
     slots return distinct instances with distinct `BatchExportProcessorOptions`
     states (by mutating one slot's configuration and asserting the other
     is unaffected).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins named-options isolation. No
     planned change. Coverage index:
     log-record-export-processor-options.all-properties.named-options"
   - Risk vs reward: low brittleness; closes the named-options gap
     identified in Section 2.4.

### 3.3 Invalid-input characterisation (guards Issue 1)

5. **`LogRecordExportProcessorOptions_ExportProcessorType_UnknownEnum_SilentlyCreatesBatchProcessor`**
   (new; `OtlpLogExporterTests.cs`).
   - Tier 2. Mechanism: InternalAccessor (`LoggerProviderSdk.Processor`
     cast). Sets `ExportProcessorType = (ExportProcessorType)99` via a
     `Configure<LogRecordExportProcessorOptions>` delegate, builds the
     provider, and asserts the built processor is a
     `BatchLogRecordExportProcessor` (the else branch in
     `BuildOtlpLogExporter`).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins silent accept; expected to
     change under Issue 1 (IValidateOptions<T> + ValidateOnStart).
     Observation: InternalAccessor - LoggerProviderSdk.Processor cast.
     Coverage index:
     log-record-export-processor-options.export-processor-type.invalid-input"
   - Risk vs reward: low effort for a high-value characterisation of a
     silent failure that Issue 1 will fix.

6. **`LogRecordExportProcessorOptions_BatchExportProcessorOptions_NullAssignment_ThrowsArgumentNullException`**
   (new; `OtlpLogExporterTests.cs`).
   - Tier 1. Mechanism: DirectProperty (exception). Calls
     `new LogRecordExportProcessorOptions { BatchExportProcessorOptions = null! }`
     and asserts `ArgumentNullException`. Pins the setter guard.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins Guard.ThrowIfNull on setter.
     No planned change to throw behaviour. Coverage index:
     log-record-export-processor-options.batch-export-processor-options.null-setter"
   - Risk vs reward: very low effort; documents the guard that already
     exists so Issue 1 does not accidentally weaken it.

### 3.4 Consumer-observed gaps

7. **`LogRecordExportProcessorOptions_BatchOptions_MaxQueueSize_ReachesProcessor`**,
   **`_ExporterTimeoutMilliseconds_ReachesProcessor`**,
   **`_MaxExportBatchSize_ReachesProcessor`** (three new tests or one
   Theory; `OtlpLogExporterTests.cs`).
   - Tier 2. Mechanism: InternalAccessor (`LoggerProviderSdk.Processor`
     cast to `BatchLogRecordExportProcessor`; public properties
     `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` - confirm
     availability via `BatchExportActivityProcessorOptionsTests` precedent;
     `MaxQueueSize` may require reflection if no public accessor exists).
   - Guards Issues 1, 21.
   - Code-comment hint: "BASELINE: pins that BatchExportProcessorOptions
     sub-properties reach the built processor. Expected to change under
     Issue 21 (OnChange for batch intervals). Observation: InternalAccessor
     + Reflection fallback for MaxQueueSize. Coverage index:
     log-record-export-processor-options.batch-export-processor-options.consumer-effect"
   - Risk vs reward: low to moderate effort; closes three consumer gaps
     that Issue 21 will touch.

8. **`LogRecordExportProcessorOptions_UseOtlpExporter_MissingLogging_ThrowsInvalidOperationException`**
   (new; `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: Exception. Attempts to use `UseOtlpExporter` with
     logging enabled but without registering
     `LogRecordExportProcessorOptions` in DI (bypass
     `AddOpenTelemetryLoggerProviderBuilderServices`). Asserts
     `InvalidOperationException` from
     `OtlpExporterBuilder.cs:201`.
   - Guards Issue 14.
   - Code-comment hint: "BASELINE: pins defensive null-check at
     OtlpExporterBuilder:201. No planned change. Coverage index:
     log-record-export-processor-options.all-properties.missing-di-guard"
   - Risk vs reward: moderate setup (requires bypassing normal DI
     registration); high value because the defensive throw is the only
     guard against a misconfigured pipeline for logging.

### 3.5 Reload no-op baseline

Shared pattern specified in
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

9. **`LogRecordExportProcessorOptions_Reload_DoesNotChangeBuiltProcessorType`**
   (new; `OtlpLogExporterTests.cs`). Tier 2. Mechanism: InternalAccessor +
   DI. Use `AddOtlpExporter` with an in-memory `IConfiguration`. Build the
   provider. Trigger `IConfigurationRoot.Reload()`. Assert the built
   processor is still the same type.
   - Guards Issues 20, 21.
   - Code-comment hint: "BASELINE: pins no-op reload on processor type.
     Expected to flip under Issue 20 (export enable/disable kill-switch).
     Coverage index:
     log-record-export-processor-options.export-processor-type.reload-no-op"

10. **`LogRecordExportProcessorOptions_Reload_DoesNotChangeBuiltBatchTimings`**
    (new; same file). Tier 2. Mechanism: InternalAccessor. Asserts
    `BatchLogRecordExportProcessor.ScheduledDelayMilliseconds` and
    `ExporterTimeoutMilliseconds` are unchanged after
    `IConfigurationRoot.Reload()` with new values in the
    `LoggingOptions` section.
    - Guards Issues 20, 21.
    - Code-comment hint: "BASELINE: pins no-op reload on batch timings.
      Expected to flip under Issue 21 (OnChange for batch intervals).
      Coverage index:
      log-record-export-processor-options.batch-export-processor-options.reload-no-op"

Risk vs reward for 3.5: moderate effort; high value - without these tests,
Issues 20 and 21 have no visible test delta when they land.

### Prerequisites and dependencies

- 3.2 item 3 depends on the env-var and options-priority isolation pattern
  decision (entry-doc Section 5); the test does not set env vars but does
  depend on DI registration ordering.
- 3.4 item 7 (`MaxQueueSize`) may need a public accessor or reflection
  depending on the `BatchLogRecordExportProcessor` API surface; check
  before authoring (see the precedent in `BatchExportActivityProcessorOptionsTests`
  for the same field in the trace processor).
- 3.4 item 8 requires understanding how to suppress normal
  `AddOpenTelemetryLoggerProviderBuilderServices` registration; confirm
  with maintainers whether the test should use a minimal hand-built DI
  container or disable the extension through a test hook.
- 3.5 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  landing first so the shared template is established.
- 3.1 item 2 (snapshot) depends on the snapshot-library selection
  (entry-doc Appendix A).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3.
- **Issue 14** - Register OTLP exporter component factories. Guarded by:
  Section 3.4 item 8 (missing-logging-registration defensive throw).
- **Issue 20** - Export enable/disable kill-switch via `OnChange` in
  `BatchExportProcessor`. Guarded by: Section 3.5 items 9 and 10.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Sections 3.4 items 7 and 10, 3.5 items 9 and 10.

Reciprocal "Baseline tests required" lines should be added to each of the
issues above, citing this file. Those edits happen in the final
cross-reference sweep, not here.
