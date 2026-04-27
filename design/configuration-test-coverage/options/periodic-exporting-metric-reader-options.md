# PeriodicExportingMetricReaderOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:16`.
- Env-var constant `OTelMetricExportIntervalEnvVarKey` -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:18`.
- Env-var constant `OTelMetricExportTimeoutEnvVarKey` -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:19`.
- Public parameterless constructor (builds its own env-var-backed
  `IConfiguration`) -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:24-27`.
- Internal constructor that takes `IConfiguration` -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:29-40`.
- `ExportIntervalMilliseconds` (nullable `int`; default `null` -> resolved
  downstream) -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:47`.
- `ExportTimeoutMilliseconds` (nullable `int`; default `null` -> resolved
  downstream) -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:54`.
- `MetricReaderOptions` host (holds `PeriodicExportingMetricReaderOptions` as
  a property; `TemporalityPreference` belongs to `MetricReaderOptions`, not
  this class) -
  `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:12-56`.
- `MetricReaderOptions.PeriodicExportingMetricReaderOptions` setter guard -
  `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:47`
  (`Guard.ThrowIfNull`).

### Consumer: `PeriodicExportingMetricReaderHelper`

The primary consumer that translates options into the running reader:

- Helper method and default fallback constants (`DefaultExportIntervalMilliseconds`
  = 60000, `DefaultExportTimeoutMilliseconds` = 30000) -
  `src/Shared/PeriodicExportingMetricReaderHelper.cs:8-9`.
- `CreatePeriodicExportingMetricReader` - reads
  `options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds` and
  `ExportTimeoutMilliseconds`, falling back to defaults when `null`, and passes
  both to `PeriodicExportingMetricReader`'s constructor along with
  `options.TemporalityPreference` and `options.DefaultHistogramAggregation` -
  `src/Shared/PeriodicExportingMetricReaderHelper.cs:11-27`.

### Consumer: `PeriodicExportingMetricReader`

- Constructor: validates with `Guard.ThrowIfInvalidTimeout` and
  `Guard.ThrowIfZero` (both properties), stores as `internal readonly int
  ExportIntervalMilliseconds` and `ExportTimeoutMilliseconds`, creates and
  starts a worker -
  `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReader.cs:30-50`.
- Internal fields `ExportIntervalMilliseconds` (line 19) and
  `ExportTimeoutMilliseconds` (line 20): directly accessible from tests because
  `OpenTelemetry.Tests` has `InternalsVisibleTo` wiring (Session 0a Sec.4.G).

### Timer mechanism note

The export interval is **not** implemented with `System.Threading.Timer`. The
thread-based worker (`PeriodicExportingMetricReaderThreadWorker`) uses
`WaitHandle.WaitAny(triggers, timeout)` where `timeout` is recalculated from a
`Stopwatch` at every iteration
(`src/OpenTelemetry/Internal/PeriodicExportingMetricReaderThreadWorker.cs:112`).
The task-based worker (`PeriodicExportingMetricReaderTaskWorker`) uses
`Task.Delay`
(`src/OpenTelemetry/Internal/PeriodicExportingMetricReaderTaskWorker.cs:130`).
Both workers receive the interval at construction and have no change mechanism.
**Reload baseline:** a running reader cannot update its export interval without
being destroyed and recreated. This is the restart-required baseline that
Issue 21 is expected to change.

### Consumer: `OtlpMetricExporterExtensions` (`AddOtlpExporter`)

- Resolves `IOptionsMonitor<MetricReaderOptions>` from DI and passes it to
  `PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporterExtensions.cs:88-89`,
  `:192-194`.
- `OtlpServiceCollectionExtensions.AddOtlpExporterMetricsServices` registers a
  `Configure<MetricReaderOptions>` that reads
  `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` (into
  `MetricReaderOptions.TemporalityPreference`, **not** into
  `PeriodicExportingMetricReaderOptions`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:20-44`.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

Projects abbreviated:

- `OT` = `test/OpenTelemetry.Tests/`
- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`

### 1.1 `Internal/PeriodicExportingMetricReaderHelperTests.cs` (OT)

These 9 tests directly exercise `PeriodicExportingMetricReaderOptions` and
`PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader`.
Env-var isolation: class-level `IDisposable` snapshot/restore (Sec.2.A of
`existing-tests.md`; no `[Collection]` attribute).

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_Defaults` | Default interval (60000) and timeout (30000) when no env vars or options set | InternalAccessor (`reader.ExportIntervalMilliseconds`, `reader.ExportTimeoutMilliseconds`) | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_Defaults_WithTask` | Same defaults under task-based worker (threading disabled via `ThreadingHelper`) | InternalAccessor | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_TemporalityPreference_FromOptions` | `MetricReaderOptions.TemporalityPreference = Delta` applied to reader | InternalAccessor (`reader.TemporalityPreference`) | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions` | Programmatic `ExportIntervalMilliseconds` beats env var (env var set to 88888, option set to 123) | InternalAccessor | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions` | Programmatic `ExportTimeoutMilliseconds` beats env var (env var set to 99999, option set to 456) | InternalAccessor | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar` | `OTEL_METRIC_EXPORT_INTERVAL` env var applied (789) | InternalAccessor | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar` | `OTEL_METRIC_EXPORT_TIMEOUT` env var applied (246) | InternalAccessor | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.CreatePeriodicExportingMetricReader_FromIConfiguration` | `IConfiguration` with both keys sets options correctly | DirectProperty (on `PeriodicExportingMetricReaderOptions` instance) | Class-level `IDisposable` |
| `PeriodicExportingMetricReaderHelperTests.EnvironmentVariableNames` | Constant values of `OTEL_METRIC_EXPORT_INTERVAL` and `OTEL_METRIC_EXPORT_TIMEOUT` | DirectProperty (constant strings) | Not env-var dependent |

### 1.2 Indirect coverage: OTLP metrics temporality theories (OTPT)

These tests exercise construction of `MetricReaderOptions` (and its nested
`PeriodicExportingMetricReaderOptions`) via the OTLP `AddOtlpExporter` DI
pathway. They are included here because `PeriodicExportingMetricReaderOptions`
is constructed as part of `MetricReaderOptions` during these tests. They do
**not** directly assert `ExportIntervalMilliseconds` or
`ExportTimeoutMilliseconds`; they assert `MetricReaderOptions.TemporalityPreference`.
They are marked as indirect coverage throughout this file.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpMetricsExporterTests.TestAddOtlpExporter_SetsCorrectMetricReaderDefaults` | Default `MetricReaderOptions` values after `AddOtlpExporter()` (indirect: constructs `PeriodicExportingMetricReaderOptions` with default env vars) | Reflection (`MeterProviderSdk.Reader` via `BindingFlags.NonPublic`) | `[Collection("EnvVars")]` |
| `OtlpMetricsExporterTests.TestTemporalityPreferenceUsingConfiguration` (Theory: 4 cases) | `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` via `IConfiguration` sets `MetricReaderOptions.TemporalityPreference`; constructs reader indirectly | DI `PostConfigure<MetricReaderOptions>` assertion | `[Collection("EnvVars")]` |
| `OtlpMetricsExporterTests.TestTemporalityPreferenceUsingEnvVar` (Theory: 4 cases) | Same preference via env var | DI `PostConfigure<MetricReaderOptions>` assertion | `[Collection("EnvVars")]`, env var set inline (no snapshot restore in this method) |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` (Theory: named/unnamed) | `ConfigureMetricsReaderOptions` sets `TemporalityPreference` and `ExportIntervalMilliseconds = 1001` on `MetricReaderOptions`; indirectly constructs `PeriodicExportingMetricReaderOptions` | DI `IOptionsMonitor<MetricReaderOptions>.Get(name)` | `[Collection("EnvVars")]` + class-level `IDisposable` |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` (Theory: named/unnamed) | `IConfiguration` binding sets `ExportIntervalMilliseconds = 1001` via `MetricsOptions:PeriodicExportingMetricReaderOptions:ExportIntervalMilliseconds`; indirectly constructs `PeriodicExportingMetricReaderOptions` | DI `IOptionsMonitor<MetricReaderOptions>.Get(name)` | `[Collection("EnvVars")]` + class-level `IDisposable` |

---

## 2. Scenario checklist and gap analysis

Status: **covered**, **partial**, or **missing**. "Currently tested by" cites
tests from Section 1 or "--" for none.

### 2.1 Named options

`PeriodicExportingMetricReaderOptions` does not use named options directly. It
is constructed fresh via `new PeriodicExportingMetricReaderOptions()` (the
public constructor) as a property inside `MetricReaderOptions`. Named-options
dispatch is at the `MetricReaderOptions` level.

**N/A - single instance per `MetricReaderOptions` host.** There is no
named-options scenario to test on this class in isolation.

### 2.2 Constructor env-var reads

The public constructor reads `OTEL_METRIC_EXPORT_INTERVAL` and
`OTEL_METRIC_EXPORT_TIMEOUT` once, at construction time. There is no reload
path.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportIntervalMilliseconds` from `OTEL_METRIC_EXPORT_INTERVAL` | `CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar` | Integer parsed; stored as `ExportIntervalMilliseconds` | **covered** |
| `ExportTimeoutMilliseconds` from `OTEL_METRIC_EXPORT_TIMEOUT` | `CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar` | Integer parsed; stored as `ExportTimeoutMilliseconds` | **covered** |
| Both env vars unset -> both properties `null` | `CreatePeriodicExportingMetricReader_Defaults` (observed via helper defaults, not via raw options) | Both `null`; helper substitutes 60000 and 30000 | **partial** (default verified at helper output level; the `null` value of each raw property is not asserted directly) |

### 2.3 Priority order

The effective priority for interval and timeout is: programmatic
`ExportIntervalMilliseconds` / `ExportTimeoutMilliseconds` (non-null) >
`IConfiguration` (including env vars read at construction) > `null` -> helper
default. The "Configure<T> vs env var" ordering is the standard DI ordering
mediated by `MetricReaderOptions` and its `PeriodicExportingMetricReaderOptions`
property.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic `ExportIntervalMilliseconds` beats `OTEL_METRIC_EXPORT_INTERVAL` env var | `CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions` | Null-coalescing in helper: non-null option wins | **covered** |
| Programmatic `ExportTimeoutMilliseconds` beats `OTEL_METRIC_EXPORT_TIMEOUT` env var | `CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions` | Same | **covered** |
| `IConfiguration` (appsettings-shaped) binding sets both properties | `CreatePeriodicExportingMetricReader_FromIConfiguration` (via internal ctor), `UseOtlpExporterConfigurationTest` (via DI `MetricReaderOptions` binding) | Internal ctor reads the named keys from `IConfiguration` | **covered** |
| `Configure<MetricReaderOptions>` delegate beats env var (DI pathway) | `UseOtlpExporterConfigureTest` (sets `ExportIntervalMilliseconds = 1001` while env var absent) | `Configure<T>` wins; not tested with env var also set | **partial** (delegate wins when env var absent; no test with env var also present confirms order) |
| Factory default (60000 / 30000) applied when options properties are `null` | `CreatePeriodicExportingMetricReader_Defaults` (via reader fields) | Helper null-coalesces to constants | **covered** (at helper-output level) |

### 2.4 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Both properties `null` at construction with no env vars | `CreatePeriodicExportingMetricReader_Defaults` (indirect: asserts 60000/30000 on the reader, not `null` on the options) | `null` | **partial** (asserted at consumer not at raw options instance) |
| Stable snapshot of `PeriodicExportingMetricReaderOptions` default shape | -- | Both `null`; no other properties | **missing** (candidate for snapshot-library pilot) |

### 2.5 Invalid-input characterisation

For each property, what happens today when input is malformed or out of range?

| Property | Input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `ExportIntervalMilliseconds` | Env var non-integer string | `TryGetIntValue` returns false; property stays `null`; no log observed in tests | -- | **missing** (silent drop; no test pins the null-retention) |
| `ExportIntervalMilliseconds` | Env var negative integer | Parsed and stored as negative; `Guard.ThrowIfZero` and `Guard.ThrowIfInvalidTimeout` in `PeriodicExportingMetricReader` constructor then throw | -- | **missing** (throw happens at reader construction, not at options level; no test pins this path) |
| `ExportIntervalMilliseconds` | Env var zero | Stored as 0; `Guard.ThrowIfZero` throws at reader construction | -- | **missing** |
| `ExportIntervalMilliseconds` | Programmatic negative value | Stored; `Guard.ThrowIfInvalidTimeout` / `Guard.ThrowIfZero` throw at reader construction | -- | **missing** |
| `ExportIntervalMilliseconds` | Programmatic zero | Stored; `Guard.ThrowIfZero` throws at reader construction | -- | **missing** |
| `ExportTimeoutMilliseconds` | Env var non-integer string | `TryGetIntValue` returns false; property stays `null` | -- | **missing** |
| `ExportTimeoutMilliseconds` | Env var negative integer | Parsed; `Guard.ThrowIfInvalidTimeout` throws at reader construction | -- | **missing** |
| `ExportTimeoutMilliseconds` | Programmatic negative value | Stored; `Guard.ThrowIfInvalidTimeout` throws at reader construction | -- | **missing** |
| `MetricReaderOptions.PeriodicExportingMetricReaderOptions` setter | Programmatic `null` | `Guard.ThrowIfNull` throws immediately | -- | **missing** (no test pins this guard) |

All **missing** invalid-input rows are expected to change under Issue 1
(`IValidateOptions<T>` + `ValidateOnStart`). Tests added here pin today's
silent-accept or deferred-throw behaviour so Issue 1 produces a visible delta.

### 2.6 Consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ExportIntervalMilliseconds` (via helper default) -> `PeriodicExportingMetricReader.ExportIntervalMilliseconds` | `CreatePeriodicExportingMetricReader_Defaults`, `_ExportIntervalMilliseconds_FromOptions`, `_FromEnvVar` | Copied directly from helper argument | **covered** |
| `ExportTimeoutMilliseconds` (via helper default) -> `PeriodicExportingMetricReader.ExportTimeoutMilliseconds` | `CreatePeriodicExportingMetricReader_Defaults`, `_ExportTimeoutMilliseconds_FromOptions`, `_FromEnvVar` | Copied directly | **covered** |
| `ExportIntervalMilliseconds = null` -> helper applies 60000 | `CreatePeriodicExportingMetricReader_Defaults` (reader value asserted) | Null-coalescing in helper | **covered** |
| `ExportTimeoutMilliseconds = null` -> helper applies 30000 | `CreatePeriodicExportingMetricReader_Defaults` (reader value asserted) | Null-coalescing in helper | **covered** |
| `ExportIntervalMilliseconds` in DI pathway (`AddOtlpExporter`) -> built reader uses correct interval | `TestAddOtlpExporter_SetsCorrectMetricReaderDefaults` (indirect; checks reader defaults, not a custom value) | Same helper path | **partial** (default only; no DI-pathway test for a non-default interval flowing through to the built reader) |
| `ExportTimeoutMilliseconds` in DI pathway -> built reader uses correct timeout | -- | Same helper path | **missing** |
| Both workers (thread and task) apply the same interval and timeout | `CreatePeriodicExportingMetricReader_Defaults` (thread) and `CreatePeriodicExportingMetricReader_Defaults_WithTask` (task) | Both receive same constructor arguments | **covered** |

### 2.7 Reload no-op baseline

Today, `PeriodicExportingMetricReaderOptions` does not participate in reload.
The public constructor reads env vars once at construction. The running worker
receives its interval at construction and cannot change it (the worker uses
`WaitHandle.WaitAny` with a computed `timeout` argument, or `Task.Delay`; there
is no `Timer.Change` call path). A reload of `IConfiguration` does not affect a
running `PeriodicExportingMetricReader`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` -> `IOptionsMonitor<MetricReaderOptions>.OnChange` fires -> running reader's `ExportIntervalMilliseconds` unchanged | -- | No reload wiring on this class | **missing** |
| `IConfigurationRoot.Reload()` -> running reader's `ExportTimeoutMilliseconds` unchanged | -- | No reload wiring | **missing** |
| `IOptionsMonitor<MetricReaderOptions>.OnChange` subscription fires on reload (confirming notification path works) | -- | Not verified | **missing** |

All three rows are expected to flip under Issue 21 (wire `OnChange` for metric
export intervals) when `Timer.Change`-equivalent reload support is added to the
worker.

---

## 3. Recommendations

One recommendation per scenario marked partial or missing. Each targets a
reviewable PR unit. Test names follow the dominant `Subject_Condition_Expected`
convention (Session 0a Sec.5.A). Target location is the existing test file for
the scenario unless noted. Tier mapping per entry-doc Section 3.
Observation-mechanism labels match entry-doc Section 2.

### 3.1 Default-state and raw property baseline

1. **`PeriodicExportingMetricReaderOptions_Defaults_PropertiesAreNull`** (new
   test in `Internal/PeriodicExportingMetricReaderHelperTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs
     `new PeriodicExportingMetricReaderOptions()` with no env vars set (class
     fixture already clears them) and asserts both properties are `null`.
   - Guards Issues 1, 17, 21. Risks pinned: `2.1`.
   - Code-comment hint:
     ```
     // BASELINE: pins current behaviour. No planned change.
     // Observation: DirectProperty - both nullable properties are null by default.
     // Coverage index: periodic-exporting-metric-reader-options.defaults.null-properties
     ```
   - Risk vs reward: trivial to write; closes the gap where only the helper
     output is currently asserted, not the raw options shape.

2. **`PeriodicExportingMetricReaderOptions_Default_Snapshot`** (new; same file
   or a dedicated `Snapshots/` subfolder per snapshot-library choice in
   entry-doc Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD; snapshot the default-constructed
     `PeriodicExportingMetricReaderOptions` object).
   - Guards Issues 1, 21.
   - Code-comment hint:
     ```
     // BASELINE: pins whole-options shape. No planned change.
     // Observation: Snapshot - covers both properties in one assertion.
     // Coverage index: periodic-exporting-metric-reader-options.all-properties.default
     ```
   - Risk vs reward: low per-test cost once the library is chosen; depends on
     snapshot-library decision (entry-doc Appendix A).

### 3.2 Priority order gap

1. **`PeriodicExportingMetricReaderOptions_ConfigureDelegate_BeatsEnvVar`**
   (new; `Internal/PeriodicExportingMetricReaderHelperTests.cs`).
   - Tier 2. Mechanism: InternalAccessor. Sets
     `OTEL_METRIC_EXPORT_INTERVAL` env var to 88888; builds a DI container with
     `AddOtlpExporter` and a `Configure<MetricReaderOptions>` delegate that sets
     `ExportIntervalMilliseconds = 123`; resolves
     `IOptionsMonitor<MetricReaderOptions>` and then calls
     `PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader`;
     asserts `reader.ExportIntervalMilliseconds == 123`.
   - Guards Issues 1, 17, 21.
   - Code-comment hint:
     ```
     // BASELINE: pins Configure<T> > env var order for ExportIntervalMilliseconds.
     // Expected to remain true under Issue 21 (interval reload) but assertion
     // is on steady state before reload.
     // Observation: InternalAccessor - reads ExportIntervalMilliseconds from reader.
     // Coverage index: periodic-exporting-metric-reader-options.export-interval.configure-beats-env-var
     ```
   - Risk vs reward: moderate setup; pins a load-bearing precedence row that
     `UseOtlpExporterConfigureTest` only covers with an absent env var.

### 3.3 Invalid-input characterisation (guards Issue 1)

All tests in this group carry the code comment:
"Expected to change under Issue 1 (`IValidateOptions<T>` for reload protection; deferred; no `ValidateOnStart`)."

1. **`PeriodicExportingMetricReaderOptions_ExportIntervalMilliseconds_InvalidEnvVar_IsNull`**
   (new; `Internal/PeriodicExportingMetricReaderHelperTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Sets
     `OTEL_METRIC_EXPORT_INTERVAL` to `"not-a-number"`. Constructs
     `new PeriodicExportingMetricReaderOptions()`. Asserts
     `ExportIntervalMilliseconds == null` (silent drop).
   - Guards Issue 1.
   - Risk vs reward: low effort; pins the silent-drop path that Issue 1 is
     expected to make an error.

2. **`PeriodicExportingMetricReaderOptions_ExportTimeoutMilliseconds_InvalidEnvVar_IsNull`**
   (new; same file). Same pattern for `OTEL_METRIC_EXPORT_TIMEOUT`.
   - Guards Issue 1.

3. **`PeriodicExportingMetricReaderOptions_ExportIntervalMilliseconds_Negative_ThrowsAtReaderConstruction`**
   and
   **`PeriodicExportingMetricReaderOptions_ExportIntervalMilliseconds_Zero_ThrowsAtReaderConstruction`**
   (new; same file).
   - Tier 1. Mechanism: Exception (via `CreatePeriodicExportingMetricReader`
     helper or direct `PeriodicExportingMetricReader` constructor). Asserts
     `ArgumentOutOfRangeException` (from `Guard.ThrowIfInvalidTimeout` /
     `Guard.ThrowIfZero`) is thrown at reader construction, not at options
     construction.
   - Code-comment hint: "BASELINE: pins deferred-throw behaviour. Expected to
     change under Issue 1 which will surface the error at `ValidateOnStart`
     instead."
   - Guards Issue 1.
   - Risk vs reward: low effort; makes the deferred-throw path explicit so
     Issue 1 produces a visible delta.

4. **`PeriodicExportingMetricReaderOptions_ExportTimeoutMilliseconds_Negative_ThrowsAtReaderConstruction`**
   (new; same file). Same pattern for timeout.
   - Guards Issue 1.

5. **`MetricReaderOptions_PeriodicExportingMetricReaderOptions_NullAssignment_Throws`**
   (new; `Internal/PeriodicExportingMetricReaderHelperTests.cs` or a dedicated
   `MetricReaderOptionsTests.cs` if one is created).
   - Tier 1. Mechanism: Exception (DirectProperty). Constructs
     `new MetricReaderOptions()` and assigns
     `options.PeriodicExportingMetricReaderOptions = null!`. Asserts
     `ArgumentNullException`.
   - Guards Issue 1. Risk vs reward: trivial; pins a guard that exists but is
     untested.

### 3.4 Consumer-observed effect in DI pathway

1. **`PeriodicExportingMetricReaderOptions_ExportInterval_FlowsToBuiltReader_ViaDI`**
   (new; `OtlpMetricsExporterTests.cs` or
   `Internal/PeriodicExportingMetricReaderHelperTests.cs`).
   - Tier 2. Mechanism: InternalAccessor (`reader.ExportIntervalMilliseconds`
     via reflection on `MeterProviderSdk.Reader`, same path used in
     `TestAddOtlpExporter_SetsCorrectMetricReaderDefaults`). Builds a
     `MeterProvider` via `AddOtlpExporter` with a `Configure<MetricReaderOptions>`
     that sets `ExportIntervalMilliseconds = 5000`. Asserts the reader field.
   - Guards Issues 1, 17, 21. Risks pinned: `2.1`.
   - Code-comment hint:
     ```
     // BASELINE: pins ExportIntervalMilliseconds flow through DI to built reader.
     // Expected to change under Issue 21 (reload support for metric intervals).
     // Observation: Reflection - MeterProviderSdk.Reader is private; field name
     //   may change under refactor.
     // Coverage index: periodic-exporting-metric-reader-options.export-interval.consumer-effect-di
     ```
   - Risk vs reward: moderate reflection brittleness; high value because it
     closes the DI-pathway consumer gap and directly guards Issue 21.

2. **`PeriodicExportingMetricReaderOptions_ExportTimeout_FlowsToBuiltReader_ViaDI`**
   (new; same file as above). Same approach for `ExportTimeoutMilliseconds`.
   - Guards Issues 1, 21.
   - Risk vs reward: same as above; pairs with interval test as a unit.

### 3.5 Reload no-op baseline

Shared pathway spec: see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

1. **`PeriodicExportingMetricReaderOptions_ReloadOfConfiguration_DoesNotChangeBuiltReaderInterval`**
   (new; `OtlpMetricsExporterTests.cs`).
   - Tier 2. Mechanism: InternalAccessor + DI. Builds a `MeterProvider` via
     `AddOtlpExporter` backed by an `InMemoryCollection` `IConfiguration`.
     Calls `IConfigurationRoot.Reload()` with a changed interval value. Asserts
     the built reader's `ExportIntervalMilliseconds` is unchanged from the
     pre-reload value.
   - Guards Issues 17, 21.
   - Code-comment hint:
     ```
     // BASELINE: pins restart-required behaviour for export interval.
     // Expected to flip under Issue 21 (Timer.Change-equivalent reload for
     // metric export intervals).
     // Observation: InternalAccessor - ExportIntervalMilliseconds field.
     // Coverage index: periodic-exporting-metric-reader-options.export-interval.reload-no-op
     ```
   - Risk vs reward: moderate setup (DI + in-memory reload); high value as
     the delta detector for Issue 21.

2. **`PeriodicExportingMetricReaderOptions_ReloadOfConfiguration_DoesNotChangeBuiltReaderTimeout`**
   (new; same file). Same pattern for timeout. Guards Issues 17, 21.

3. **`PeriodicExportingMetricReaderOptions_OnChangeSubscription_FiresOnReload_ButReaderUnchanged`**
   (new; same file). Tier 2. Mechanism: DI + subscription assertion. Registers
   `IOptionsMonitor<MetricReaderOptions>.OnChange` callback before reload;
   asserts the callback fires while the reader fields remain unchanged. Guards
   Issue 17.

### Prerequisites and dependencies

- 3.2 depends on the env-var isolation pattern decision (entry-doc Section 5);
  new tests setting env vars in a Tier 2 DI test need fixture or `[Collection]`
  grouping.
- 3.5 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  landing first so the tests follow a shared template.
- 3.1.2 depends on the snapshot-library selection
  ([entry-doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.4.
- **Issue 17** - Design and implement standard `OnChange` subscriber pattern.
  Guarded by: Section 3.5.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Sections 3.2, 3.4, 3.5.

Reciprocal "Baseline tests required" lines should be added to each of the
issues above citing this file. Those edits happen in the final
cross-reference sweep, not here.
