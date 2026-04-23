# MetricReaderOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration and public constructor -
  `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:12-56`.
- Public parameterless constructor delegates to internal constructor -
  `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:19-22`.
- Internal constructor accepting
  `defaultPeriodicExportingMetricReaderOptions` -
  `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:24-32`.
- Property declarations:
  - `TemporalityPreference` (default `Cumulative`) -
    `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:37`.
  - `PeriodicExportingMetricReaderOptions` (get + null-guarded set) -
    `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:42-50`.
  - `DefaultHistogramAggregation` (internal, nullable) -
    `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:55`.
- **No env-var reads in the constructor.** `MetricReaderOptions` itself
  has no env-var binding. Env-var and `IConfiguration` binding is applied
  externally via a named `Configure<MetricReaderOptions>` registered by
  `AddOtlpExporterMetricsServices` (see below).

### Registration and env-var binding site

`OtlpServiceCollectionExtensions.AddOtlpExporterMetricsServices` -
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:16-45`.

This method calls
`services.AddOptions<MetricReaderOptions>(name).Configure<IConfiguration>(...)`,
which reads:

- `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` ->
  `TemporalityPreference` (via
  `OtlpSpecConfigDefinitions.MetricsTemporalityPreferenceEnvVarName` at
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:28`).
- `OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION` ->
  `DefaultHistogramAggregation` (via
  `OtlpSpecConfigDefinitions.MetricsDefaultHistogramAggregationEnvVarName`
  at `OtlpSpecConfigDefinitions.cs:29`).

`PeriodicExportingMetricReaderOptions` interval and timeout are NOT bound
here; they are bound separately by `PeriodicExportingMetricReaderOptions`
own constructor reading `OTEL_METRIC_EXPORT_INTERVAL` /
`OTEL_METRIC_EXPORT_TIMEOUT`. See the companion
[periodic-exporting-metric-reader-options.md](periodic-exporting-metric-reader-options.md)
file for those bindings.

The `UseOtlpExporter` pathway also calls
`services.Configure<MetricReaderOptions>(name, configuration.GetSection("MetricsOptions"))` -
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:158-159`.

### Direct consumer sites

- `OtlpMetricExporterExtensions.AddOtlpExporter` (both overloads) resolves
  `IOptionsMonitor<MetricReaderOptions>.Get(finalOptionsName)` and passes it
  to `BuildOtlpExporterMetricReader` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporterExtensions.cs:88`
  and `:145`.
- `OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader` accepts the
  resolved instance and uses `metricReaderOptions.TemporalityPreference`,
  `metricReaderOptions.PeriodicExportingMetricReaderOptions`, and
  `metricReaderOptions.DefaultHistogramAggregation` to construct the metric
  reader -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporterExtensions.cs:157-163`.
- `OtlpExporterBuilderOptions` holds the resolved `MetricReaderOptions`
  reference (field `MetricReaderOptions`) for use during
  `UseOtlpExporter` pipeline construction -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:18`.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

The entry in `existing-tests.md` (inventory table row 11) states coverage
is "resolved in `UseOtlpExporterConfigurationTest` theories". This means
coverage of `MetricReaderOptions` is **indirect** - the option is resolved
inside larger theory-driven tests that cover the full `UseOtlpExporter`
pipeline, not tests that exercise `MetricReaderOptions` in isolation.

Project abbreviations:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | `ConfigureMetricsReaderOptions` delegate sets `TemporalityPreference` and `PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds` (Theory, named + unnamed) | DI `IOptionsMonitor<MetricReaderOptions>.Get(name)` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` | `UseOtlpExporter(IConfiguration)` binds `MetricsOptions` section into named `MetricReaderOptions` (Theory, named + unnamed) | DI `IOptionsMonitor<MetricReaderOptions>.Get(Options.DefaultName)` via `OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsTest` | `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` env var applied to resolved `MetricReaderOptions` (Theory) | DI `IOptionsMonitor<MetricReaderOptions>` (asserted through `OtlpSpecConfigDefinitionTests`) | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | Same via `IConfiguration` (Theory) | DI `IOptionsMonitor<MetricReaderOptions>` | `[Collection]` attribute |

The four tests above are the only tests that exercise `MetricReaderOptions`
through the configuration pipeline. There are no tests that:

- Construct `MetricReaderOptions` directly and assert property defaults.
- Test the `AddOtlpExporter` (non-`UseOtlpExporter`) pathway for env-var
  binding of `TemporalityPreference` or `DefaultHistogramAggregation`.
- Test invalid-input handling for `TemporalityPreference` or
  `DefaultHistogramAggregation`.
- Test the `PeriodicExportingMetricReaderOptions = null` setter guard.
- Test reload no-op for `MetricReaderOptions`.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Constructor defaults

`MetricReaderOptions` has no env-var reads in its own constructors. The
public constructor `new MetricReaderOptions()` builds a
`PeriodicExportingMetricReaderOptions` instance with its own env-var reads
and sets the private field. Defaults observable without env vars or DI:

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `TemporalityPreference` default is `Cumulative` | - | `MetricReaderTemporalityPreference.Cumulative` | missing |
| `PeriodicExportingMetricReaderOptions` is non-null by default | - | Non-null; populated by `new PeriodicExportingMetricReaderOptions()` | missing |
| `DefaultHistogramAggregation` is null by default | - | `null` | missing |
| Setting `PeriodicExportingMetricReaderOptions = null` throws `ArgumentNullException` | - | `Guard.ThrowIfNull` throws | missing |

### 2.2 Env-var binding (via `AddOtlpExporterMetricsServices`)

Binding is applied by the named `Configure<MetricReaderOptions>` in
`OtlpServiceCollectionExtensions.AddOtlpExporterMetricsServices`, not in
the constructor. Tests must go through the DI pipeline or at minimum call
the binding lambda directly.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=delta` -> `TemporalityPreference = Delta` via `AddOtlpExporter` pathway | - | Parsed case-insensitively via `Enum.TryParse`; stored | missing (covered only via `UseOtlpExporter` pathway) |
| `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=cumulative` -> `Cumulative` via `AddOtlpExporter` pathway | - | Same parsing path | missing |
| `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=lowmemory` -> `LowMemory` via `AddOtlpExporter` pathway | - | Same parsing path | missing |
| `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` unknown value -> default kept, no throw | - | `Enum.TryParse` returns false; default unchanged; no log emitted | missing (invalid-input, see 2.4) |
| `OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION=base2_exponential_bucket_histogram` -> `DefaultHistogramAggregation = Base2Exponential...` | - | String comparison (case-insensitive); stored | missing |
| `OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION=explicit_bucket_histogram` -> `ExplicitBucketHistogram` | - | String comparison; stored | missing |
| `OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION` absent -> `DefaultHistogramAggregation` stays null | - | Neither branch taken; `null` preserved | missing |
| `UseOtlpExporter` + env var `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` -> resolved `MetricReaderOptions.TemporalityPreference` | `UseOtlpExporterRespectsSpecEnvVarsTest` (Theory) | Bound via same `Configure<MetricReaderOptions>` | covered (indirect; asserted through `OtlpSpecConfigDefinitionTests.MetricsData.AssertMatches`) |
| `UseOtlpExporter(IConfiguration)` binds `MetricsOptions:TemporalityPreference` section | `UseOtlpExporterConfigurationTest` (Theory) | `services.Configure<MetricReaderOptions>(name, config.GetSection("MetricsOptions"))` | covered (indirect) |

### 2.3 Priority order

The effective priority for `TemporalityPreference` and
`DefaultHistogramAggregation` is: programmatic `Configure<T>` delegate >
`IConfiguration` section bind > env var (all three arrive via the
`Microsoft.Extensions.Options` `Configure<T>` pipeline, so order of
registration in DI determines winner) > type default (no factory reads env
vars in the constructor).

`PeriodicExportingMetricReaderOptions` interval and timeout follow the
`PeriodicExportingMetricReaderOptions` constructor's own env-var reads, not
this class's DI binding. That class is covered separately.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Configure<MetricReaderOptions>` delegate beats env var for `TemporalityPreference` | - | Delegate runs after env-var `Configure`; wins by pipeline ordering | missing |
| `IConfiguration` section binding beats env var for `TemporalityPreference` | `UseOtlpExporterConfigurationTest` (indirect; tests section binding but not priority against env var) | Both arrive via `Configure<T>` pipeline | partial |
| Type default `Cumulative` used when no env var, config, or delegate sets `TemporalityPreference` | - | Not verified via DI; only via direct construction | missing |

### 2.4 Invalid-input characterisation

Each property: what happens today when input is malformed or out of range?
Pin current behaviour so Issue 1 validation work has a visible delta.

| Property | Malformed input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `TemporalityPreference` | Env var unknown string (e.g. `"bogus"`) | `Enum.TryParse` fails; default `Cumulative` kept; no log emitted | - | missing |
| `TemporalityPreference` | Programmatic unknown enum value (e.g. `(MetricReaderTemporalityPreference)99`) | Stored as-is; downstream `MetricReader.TemporalityPreference` setter stores the value; consumer behaviour undefined for unknown values | - | missing |
| `DefaultHistogramAggregation` | Env var unknown string (e.g. `"explicit"` without suffix) | Neither branch taken; `null` preserved; no log emitted | - | missing |
| `DefaultHistogramAggregation` | Programmatic unknown enum value | Stored as-is; downstream may ignore or silently use default | - | missing |
| `PeriodicExportingMetricReaderOptions` | Programmatic `null` | `Guard.ThrowIfNull` throws `ArgumentNullException` | - | missing |

All missing invalid-input rows are expected to change under Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart` for all options classes). Tests
added here pin today's silent-accept behaviour so Issue 1 produces a
visible delta.

### 2.5 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Stable snapshot of all `MetricReaderOptions` public properties at type defaults (no env vars, no DI) | - | Not snapshotted | missing (candidate for snapshot-library pilot) |

### 2.6 Named-options

`MetricReaderOptions` has no named-options factory of its own. It is
registered via `services.AddOptions<MetricReaderOptions>(name)` by
`AddOtlpExporterMetricsServices`. There is no `RegisterOptionsFactory`
call; the standard `IOptions<T>` factory is used. Named instances are
independent.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<MetricReaderOptions>.Get("name1")` and `.Get("name2")` are independent instances with independent env-var bindings | `UseOtlpExporterConfigureTest` (Theory exercises named + unnamed; asserts values but not instance-separation directly) | Each named registration has its own `Configure<T>` chain | partial (Theory exercises distinct names; does not pin identity or independence in isolation) |
| Unnamed (`Options.DefaultName`) instance receives env-var binding from `AddOtlpExporter` unnamed call | - | `AddOtlpExporterMetricsServices(Options.DefaultName)` registers binding for `""` name | missing (no standalone test for `AddOtlpExporter` unnamed pathway) |

### 2.7 Reload no-op baseline

`MetricReaderOptions` is registered as a standard `IOptions<T>` with
`AddOptions<T>(name).Configure<IConfiguration>(...)`. The `Configure`
delegate runs once per named instance creation. There is no
`IOptionsMonitor<T>` subscriber in the built metric reader today; reload
does not propagate.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` triggers `IOptionsMonitor<MetricReaderOptions>.OnChange` but built metric reader `TemporalityPreference` is unchanged | - | Not verified | missing |
| `IConfigurationRoot.Reload()` -> `DefaultHistogramAggregation` on built reader unchanged | - | Not verified | missing |

Reload rows are expected to flip under Issue 21 (wire `OnChange` for batch
and metric export intervals) and Issue 17 (standard `OnChange` subscriber
pattern).

### 2.8 Consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `TemporalityPreference` -> `MetricReader.TemporalityPreference` on the built reader | `TemporalityPreferenceFromEnvVar`, `TemporalityPreferenceFromIConfiguration` (OTPT `OtlpMetricsExporterTests`) | `BuildOtlpExporterMetricReader` passes `TemporalityPreference` to the constructed `PeriodicExportingMetricReader` | partial (env var and `IConfiguration` paths covered via `PostConfigure<MetricReaderOptions>` probe; not via `AddOtlpExporter` unnamed pathway) |
| `PeriodicExportingMetricReaderOptions` intervals -> built `PeriodicExportingMetricReader` timings | - | Consumer reads `metricReaderOptions.PeriodicExportingMetricReaderOptions` and passes to reader constructor | missing (flow from options to built reader; see also `periodic-exporting-metric-reader-options.md`) |
| `DefaultHistogramAggregation` -> histogram aggregation selection on the built reader | - | `BuildOtlpExporterMetricReader` reads `metricReaderOptions.DefaultHistogramAggregation` | missing |

---

## 3. Recommendations

One item per gap. Test name follows `Subject_Condition_Expected` convention
from entry-doc Section 6. Target location is the existing test file for
the scenario; new files only where noted. Tier and observation-mechanism
labels match entry-doc Sections 2 and 3.

### 3.1 Constructor defaults and property guards

1. **`MetricReaderOptions_Defaults`** (new test in a new or existing
   `MetricReaderOptionsTests.cs` inside
   `test/OpenTelemetry.Tests/Metrics/`).
   - Tier 1. Mechanism: DirectProperty. Construct `new MetricReaderOptions()`
     and assert `TemporalityPreference == Cumulative`,
     `PeriodicExportingMetricReaderOptions != null`,
     `DefaultHistogramAggregation == null`.
   - No env-var isolation needed (no env-var reads in constructor).
   - Guards Issue 1. No planned change under current issues.
   - Code-comment hint: "BASELINE: pins current type defaults. No planned
     change. Observation: DirectProperty."
   - Risk vs reward: trivial effort; closes the gap that this public class
     has zero direct construction tests.

2. **`MetricReaderOptions_PeriodicExportingMetricReaderOptions_NullThrows`**
   (same file).
   - Tier 1. Mechanism: DirectProperty (exception). Calls
     `options.PeriodicExportingMetricReaderOptions = null` and asserts
     `ArgumentNullException`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins null-guard on setter. Expected to
     remain; validation strengthening under Issue 1 may add additional
     guards."
   - Risk vs reward: one-line assert; high signal-to-noise.

### 3.2 Env-var binding via `AddOtlpExporter` pathway

3. **`MetricReaderOptions_TemporalityPreference_FromEnvVar`** (new test in
   `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpMetricsExporterTests.cs`).
   - Tier 2. Mechanism: DI
     (`IOptionsMonitor<MetricReaderOptions>.Get(Options.DefaultName)`).
     Set `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=delta`, build
     `IServiceCollection` with `AddOtlpExporter()`, resolve and assert
     `TemporalityPreference == Delta`.
   - Env-var isolation: class-level `IDisposable` snapshot/restore + existing
     `[Collection("EnvVars")]` grouping already in use in the file.
   - Guards Issues 1, 17.
   - Code-comment hint: "BASELINE: pins env-var binding in `AddOtlpExporter`
     pathway (distinct from `UseOtlpExporter`). Expected to change under
     Issue 1 if validation is added."
   - Risk vs reward: moderate setup (DI + env-var fixture); pins the gap
     that env-var coverage currently only exists via `UseOtlpExporter`.

4. **`MetricReaderOptions_DefaultHistogramAggregation_FromEnvVar`** (same
   file; Theory with two `InlineData` rows for
   `base2_exponential_bucket_histogram` and `explicit_bucket_histogram`).
   - Tier 2. Mechanism: DI. Asserts `DefaultHistogramAggregation` equals
     the expected `MetricReaderHistogramAggregation` enum value.
   - Guards Issue 1.
   - Risk vs reward: low incremental effort once 3 is in place; pins a
     property that is otherwise completely uncovered.

5. **`MetricReaderOptions_DefaultHistogramAggregation_AbsentEnvVar_IsNull`**
   (same file).
   - Tier 2. Mechanism: DI. No env var set; asserts
     `DefaultHistogramAggregation == null`.
   - Guards Issue 1. No planned change.
   - Risk vs reward: trivial; prevents silent regression if the absent-value
     branch is accidentally changed.

### 3.3 Priority order

6. **`MetricReaderOptions_ConfigureDelegate_BeatsEnvVar`** (new test in
   `OtlpMetricsExporterTests.cs`).
   - Tier 2. Mechanism: DI. Set env var to `delta`, then call
     `services.Configure<MetricReaderOptions>(o => o.TemporalityPreference =
     MetricReaderTemporalityPreference.Cumulative)`. Resolve and assert
     `Cumulative`. Justifies: the `Configure<T>` delegate runs after the
     env-var `Configure` and wins.
   - Env-var isolation: class-level snapshot/restore.
   - Guards Issues 1, 17.
   - Code-comment hint: "BASELINE: pins `Configure<T>` > env var priority
     for `TemporalityPreference`."
   - Risk vs reward: low brittleness; pins a load-bearing precedence row
     that is not verified anywhere.

### 3.4 Invalid-input characterisation (guards Issue 1)

7. **`MetricReaderOptions_TemporalityPreference_UnknownEnvVar_KeepsDefault`**
   (new test in `OtlpMetricsExporterTests.cs`).
   - Tier 2. Mechanism: DI. Set
     `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=bogus_value`, resolve
     and assert `TemporalityPreference == Cumulative` (default kept; no throw).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins silent-accept for unknown temporality
     string. Expected to change under Issue 1 (validation adds log or throw)."
   - Risk vs reward: low effort; pins current silent-fallback behaviour.

8. **`MetricReaderOptions_DefaultHistogramAggregation_UnknownEnvVar_IsNull`**
   (same file).
   - Tier 2. Mechanism: DI. Set env var to `"explicit"` (valid concept,
     invalid casing for the spec string). Assert `DefaultHistogramAggregation
     == null` (neither branch matched; null preserved).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins strict string-comparison behaviour.
     Under Issue 1 this may be tightened or a log emitted."
   - Risk vs reward: low; pins important silent-fallback.

9. **`MetricReaderOptions_TemporalityPreference_UnknownEnumValue_IsAcceptedSilently`**
   (new test in a `MetricReaderOptionsTests.cs` in
   `test/OpenTelemetry.Tests/Metrics/`).
   - Tier 1. Mechanism: DirectProperty. Set
     `options.TemporalityPreference = (MetricReaderTemporalityPreference)99`
     and assert no exception and value stored as-is.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins silent-accept for unknown enum at
     assignment time. Expected to change under Issue 1."
   - Risk vs reward: trivial; pins silent-accept pattern.

### 3.5 Consumer-observed effects

10. **`MetricReaderOptions_TemporalityPreference_AppliedToBuiltReader`**
    (new test in `OtlpMetricsExporterTests.cs`).
    - Tier 2. Mechanism: DI + InternalAccessor (resolve `MeterProviderSdk`
      and read the `Reader` field already accessible via existing
      `InternalsVisibleTo` wiring; see entry-doc Sec.2.3 example).
      Set `TemporalityPreference = Delta` via `Configure<MetricReaderOptions>`,
      build the provider, assert the built reader's `TemporalityPreference`
      is `Delta`.
    - Guards Issues 1, 21.
    - Code-comment hint: "BASELINE: pins flow of `TemporalityPreference` to
      the built reader. Expected to remain stable across Issue 21."
    - Risk vs reward: moderate (reflection on `MeterProviderSdk.Reader`
      already used in codebase); closes the most important consumer-observed
      gap.

11. **`MetricReaderOptions_DefaultHistogramAggregation_AppliedToBuiltReader`**
    (same file).
    - Tier 2. Mechanism: DI + InternalAccessor. Same pattern as 10 but
      asserts `DefaultHistogramAggregation` takes effect via
      `BuildOtlpExporterMetricReader`.
    - Guards Issue 1.
    - Risk vs reward: low incremental cost once 10 is in place.

### 3.6 Reload no-op baseline

Shared pathway spec applies; see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

12. **`MetricReaderOptions_ReloadOfConfiguration_DoesNotChangeBuiltReaderTemporalityPreference`**
    (new test in `OtlpMetricsExporterTests.cs`).
    - Tier 2. Mechanism: DI + InternalAccessor + `IConfigurationRoot.Reload()`.
      Build with `TemporalityPreference = Delta`. Reload config with
      `Cumulative`. Assert built reader preference unchanged.
    - Guards Issues 17, 21.
    - Code-comment hint: "BASELINE: pins no-op reload for `MetricReaderOptions`.
      Expected to flip under Issue 21 (metric export interval `OnChange`)
      and/or Issue 17 (standard `OnChange` subscriber)."
    - Risk vs reward: moderate effort; high value - without this, Issues 17
      and 21 have no visible test delta for this class.

### 3.7 Default-state snapshot (pilot-dependent)

13. **`MetricReaderOptions_Default_Snapshot`** (new; location per the
    snapshot-library choice in entry-doc Appendix A).
    - Tier 1. Mechanism: Snapshot (library TBD by maintainers). Serialises
      a freshly constructed `MetricReaderOptions` to a checked-in file and
      compares.
    - Guards Issue 1.
    - Code-comment hint: "BASELINE: pins whole-options shape. Snapshot
      update expected on any additive change; reviewer confirms intent."
    - Risk vs reward: low once library is chosen; guards against silent
      default drift.

### Prerequisites and dependencies

- 3.2, 3.3, and 3.4 (env-var tests) depend on the env-var isolation
  pattern decision (entry-doc Section 5). The existing class-level
  `IDisposable` snapshot/restore pattern used in `OtlpMetricsExporterTests`
  is the natural fit.
- 3.6 depends on the shared
  [`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md)
  file landing first so the test follows the shared template.
- 3.7 depends on the snapshot-library selection (entry-doc Appendix A).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.4, 3.5, 3.7.
- **Issue 17** - Design and implement standard `OnChange` subscriber
  pattern. Guarded by: Sections 3.3 (priority tests use DI path that
  `OnChange` will traverse) and 3.6 (reload no-op baseline).
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Sections 3.5 and 3.6 (consumer-effect and reload baseline).

Reciprocal "Baseline tests required" lines should be added to each of
the issues above, citing this file. Those edits happen in the final
cross-reference sweep, not here.
