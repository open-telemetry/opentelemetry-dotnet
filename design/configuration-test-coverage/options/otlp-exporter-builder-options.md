# OtlpExporterBuilderOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

`OtlpExporterBuilderOptions` is internal. It is the aggregate root
created by the `UseOtlpExporter` pathway and is never constructed
directly by callers.

- Type declaration and field layout -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:13-62`.
- Five companion fields (resolved from DI at factory time):
  - `SdkLimitOptions` - line 15.
  - `ExperimentalOptions` - line 16.
  - `LogRecordExportProcessorOptions?` - line 17.
  - `MetricReaderOptions?` - line 18.
  - `ActivityExportProcessorOptions?` - line 19.
- Four named `OtlpExporterOptions` fields -
  `OtlpExporterBuilderOptions.cs:21-24`; constructed at lines 46-52.
  - `DefaultOptionsInstance` - `OtlpExporterOptionsConfigurationType.Default`.
  - `LoggingOptionsInstance` - `OtlpExporterOptionsConfigurationType.Logs`.
  - `MetricsOptionsInstance` - `OtlpExporterOptionsConfigurationType.Metrics`.
  - `TracingOptionsInstance` - `OtlpExporterOptionsConfigurationType.Traces`.
- Public `IOtlpExporterOptions` properties exposing the four instances -
  `OtlpExporterBuilderOptions.cs:55-61` (`DefaultOptions`, `LoggingOptions`,
  `MetricsOptions`, `TracingOptions`).
- `OtlpExporterBuilderOptions` internal constructor -
  `OtlpExporterBuilderOptions.cs:26-53`. Takes `IConfiguration` (passed
  through to each `OtlpExporterOptions` constructor; this is how the
  env-var-backed configuration reaches the four instances).
- `BindConfigurationToOptions` (static, private) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:120-163`.
  Called only when `UseOtlpExporter(IConfiguration)` is used; calls
  `services.Configure<OtlpExporterBuilderOptions>(name, configuration)` at
  line 153, plus three companion `services.Configure<T>` calls at lines 155-162.
  `ConfigurationBinder.Bind()` is invoked internally by `services.Configure`
  with no `[UnconditionalSuppressMessage]`; this is the IL2026/IL3050 violation
  targeted by Issue 4.
- `OtlpExporterBuilder` - four `Configure*` methods that wrap
  `services.Configure<OtlpExporterBuilderOptions>(name, ...)` and route
  the delegate to the appropriate `IOtlpExporterOptions` property:
  - `ConfigureDefaultExporterOptions` - `OtlpExporterBuilder.cs:49-58`.
  - `ConfigureLoggingExporterOptions` - `OtlpExporterBuilder.cs:60-69`.
  - `ConfigureMetricsExporterOptions` - `OtlpExporterBuilder.cs:80-89`.
  - `ConfigureTracingExporterOptions` - `OtlpExporterBuilder.cs:100-109`.
- Options-factory registration for `OtlpExporterBuilderOptions` -
  `OtlpExporterBuilder.cs:176-191`. Uses `RegisterOptionsFactory` which
  supplies `(IServiceProvider, IConfiguration, string name)` to the
  constructor.
- Cascade application (`signal.ApplyDefaults(default)`) at pipeline
  build time - `OtlpExporterBuilder.cs:200` (logging), `:219` (metrics),
  `:234` (tracing). This is the moment the `DefaultOptionsInstance` values
  are merged into each signal instance for any property `HasData == false`.
- `UseOtlpExporter` public and internal entry-point overloads -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OpenTelemetryBuilderOtlpExporterExtensions.cs:37-151`.
  All overloads resolve to the internal four-parameter overload at line 131
  which constructs `OtlpExporterBuilder`.

### Named-options story (cross-reference)

`OtlpExporterBuilderOptions` holds the four named `OtlpExporterOptions`
instances. The per-property env-var binding, `ApplyDefaults` cascade logic,
and signal-specific env-var constants are documented in
[`otlp-exporter-options.md`](otlp-exporter-options.md). This file records
only what differs in the builder layer:

- The builder is responsible for wiring the cascade (`ApplyDefaults`) at
  pipeline build time; `OtlpExporterOptions` itself does not cascade
  automatically.
- `BindConfigurationToOptions` adds a second configuration source to the
  four `OtlpExporterOptions` instances by binding the whole
  `IConfiguration` root onto `OtlpExporterBuilderOptions`; individual
  signal sections are also bound to the companion processor/reader options.
- The `OtlpExporterBuilderOptions.name` determines which `IOptionsMonitor`
  slot is used (defaults to `Options.DefaultName` when no name is given, or
  `"otlp"` when `IConfiguration` is provided but no name is specified - see
  `OtlpExporterBuilder.cs:29-39`).

### Direct consumer sites

The four `OtlpExporterOptions` instances are consumed at pipeline build time
inside `RegisterOtlpExporterServices` (static local method starting at
`OtlpExporterBuilder.cs:165`) via `ConfigureOpenTelemetry{LoggerProvider,
MeterProvider,TracerProvider}` callbacks. Each callback calls
`GetBuilderOptionsAndValidateRegistrations` (lines 246-251), which resolves
`IOptionsMonitor<OtlpExporterBuilderOptions>.Get(name)` and validates
singleton registration. The signal-specific exporter build helpers then
receive the cascaded options instance.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md) - rows for
`UseOtlpExporterExtensionTests.cs`. Inventory only.

Project abbreviation: `OTPT` =
`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

All six tests that exercise `OtlpExporterBuilderOptions` live in
`OTPT/UseOtlpExporterExtensionTests.cs`. The class is `[Collection("EnvVars")]`
and implements `IDisposable` (constructor and `Dispose` both call
`OtlpSpecConfigDefinitionTests.ClearEnvVars()`).

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterDefaultTest` | `UseOtlpExporter()` with no config: asserts `DefaultOptions.Endpoint`, `DefaultOptions.Protocol`, and `HasData == false` on all four instances | DI `IOptionsMonitor<OtlpExporterBuilderOptions>.CurrentValue` | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterSetEndpointAndProtocolTest` | `UseOtlpExporter(protocol, baseUrl)` overload sets `DefaultOptions.Protocol` and `DefaultOptions.Endpoint`; signal instances carry `HasData == false` (Theory over Grpc/HttpProtobuf) | DI `IOptionsMonitor<OtlpExporterBuilderOptions>.CurrentValue` | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | All four `Configure*` delegates plus companion processor/reader delegates; named and unnamed (Theory over `null`/"testNamedOptions") | DI `IOptionsMonitor<OtlpExporterBuilderOptions>.Get(name)` + `IOptionsMonitor<LogRecordExportProcessorOptions>.Get(name)` + `IOptionsMonitor<MetricReaderOptions>.Get(name)` + `IOptionsMonitor<ActivityExportProcessorOptions>.Get(name)` | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` | `UseOtlpExporter(IConfiguration)` binds `DefaultOptions`/`LoggingOptions`/`MetricsOptions`/`TracingOptions` sections from an in-memory `IConfiguration`; named and unnamed (Theory) | DI as above (`VerifyOptionsApplied` helper) | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsTest` | All `OTEL_EXPORTER_OTLP_*` and signal-specific env vars applied to each of the four instances | DI `IOptionsMonitor<OtlpExporterBuilderOptions>.Get(Options.DefaultName)` + `IOptionsMonitor<MetricReaderOptions>.Get(Options.DefaultName)` | `[Collection("EnvVars")]` + IDisposable (env vars set by `OtlpSpecConfigDefinitionTests.SetEnvVars()`) |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | Same assertions as above but driven from `IConfiguration` registered in DI instead of env vars | DI as above | `[Collection("EnvVars")]` + IDisposable (no env vars set; uses `OtlpSpecConfigDefinitionTests.ToConfiguration()`) |

Additional tests in `UseOtlpExporterExtensionTests.cs` that exercise
`OtlpExporterBuilderOptions` indirectly (DI composition / guard checks;
options values not asserted):

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterSingleCallsTest` | `UseOtlpExporter()` builds all three providers | DI service resolution (no options assertions) | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterMultipleCallsTest` | Second `UseOtlpExporter()` call throws `NotSupportedException` | Exception | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterLoggingTest` | `UseOtlpExporter()` + `AddOtlpExporter()` (logging) throws `NotSupportedException` | Exception | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterMetricsTest` | Same for metrics | Exception | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterWithAddOtlpExporterTracingTest` | Same for tracing | Exception | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterAddsTracingProcessorToPipelineEndTest` | OTLP processor placed after user processor in tracing pipeline | InternalAccessor (`TracerProviderSdk.Processor` cast to `CompositeProcessor<Activity>`) | `[Collection("EnvVars")]` + IDisposable |
| `UseOtlpExporterExtensionTests.UseOtlpExporterAddsLoggingProcessorToPipelineEndTest` | OTLP processor placed after user processor in logging pipeline | InternalAccessor (`LoggerProviderSdk.Processor`) | `[Collection("EnvVars")]` + IDisposable |

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or "--" for none.

### 2.1 Constructor behaviour and field initialisation

`OtlpExporterBuilderOptions` is constructed by the factory registered at
`OtlpExporterBuilder.cs:176-191`. The factory resolves companion options
from DI before constructing the instance; `ActivityExportProcessorOptions`
is read for its `BatchExportProcessorOptions` which is passed as the
`defaultBatchOptions` argument to each `OtlpExporterOptions` constructor.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Factory produces a valid `OtlpExporterBuilderOptions` instance with all four `OtlpExporterOptions` fields non-null | `UseOtlpExporterDefaultTest` (asserts `DefaultOptions.Endpoint` + `HasData` on all four) | All four instances created | covered |
| Each instance constructed with the correct `OtlpExporterOptionsConfigurationType` (Default/Logs/Metrics/Traces) | `UseOtlpExporterRespectsSpecEnvVarsTest` (signal-specific env vars asserted per instance) | `DefaultOptionsInstance` reads generic env vars; signal instances read signal-specific variants | covered |
| `SdkLimitOptions` resolved using `CurrentValue` (unnamed, singleton-like) while the four `OtlpExporterOptions` instances use the named slot | -- | `SdkLimitOptions` always `CurrentValue`; factory comment at line 178 explains the rationale | missing (behaviour not pinned by test) |
| `ExperimentalOptions` resolved using the named slot (`IOptionsMonitor<ExperimentalOptions>.Get(name)`) | -- | Resolved by name; signal-specific `ExperimentalOptions` is conceivable | missing |
| Companion options (`LogRecordExportProcessorOptions`, `MetricReaderOptions`, `ActivityExportProcessorOptions`) are nullable; factory uses `?.Get(name)` | `UseOtlpExporterConfigureTest` (asserts their values via DI) | Null when signal not enabled; comment at `OtlpExporterBuilder.cs:183` states intent | partial (values tested when signals are enabled; null path not covered) |
| `ActivityExportProcessorOptions.BatchExportProcessorOptions` is used as `defaultBatchOptions` for all four `OtlpExporterOptions` instances | -- | Line 44 reads `this.ActivityExportProcessorOptions!.BatchExportProcessorOptions`; will `NullReferenceException` if null (no null guard) | missing |
| `OtlpExporterBuilderOptions` constructor `Debug.Assert` guards (not null assertions) | -- | Debug-only; not verified in release | n/a (debug-only; not testable in release builds without mocking) |

### 2.2 `UseOtlpExporter` overloads

These are the public entry points; gaps here imply the builder is not
reached at all.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `UseOtlpExporter()` (no args): defaults applied, all signals enabled | `UseOtlpExporterDefaultTest`, `UseOtlpExporterSingleCallsTest` | Three providers built; `OtlpExporterBuilderOptions` resolved at `Options.DefaultName` | covered |
| `UseOtlpExporter(protocol, baseUrl)`: `DefaultOptions.Protocol` and `.Endpoint` set; signal instances carry `HasData == false` | `UseOtlpExporterSetEndpointAndProtocolTest` | Theory over both protocols | covered |
| `UseOtlpExporter(IConfiguration)`: sections bound to all four instances via `BindConfigurationToOptions` | `UseOtlpExporterConfigurationTest` (unnamed branch: `name` becomes `"otlp"`) | Reflection binding via `services.Configure<OtlpExporterBuilderOptions>(name, configuration)` | covered (but see Issue 4 - no AOT test) |
| `UseOtlpExporter(name, configuration, configure)`: named slot used for options isolation | `UseOtlpExporterConfigurationTest` (named branch: `name == "testNamedOptions"`) | Named `IOptionsMonitor` slot populated | covered |
| `UseOtlpExporter(Action<OtlpExporterBuilder>)`: configure callback invoked | `UseOtlpExporterConfigureTest` | All four `Configure*` delegates reachable | covered |
| `UseOtlpExporter(protocol, null)` throws `ArgumentNullException` | `UseOtlpExporterSetEndpointAndProtocolTest` (inline `Assert.Throws`) | `Guard.ThrowIfNull` at `OpenTelemetryBuilderOtlpExporterExtensions.cs:59` | covered |
| Second `UseOtlpExporter()` call throws `NotSupportedException` | `UseOtlpExporterMultipleCallsTest` | `EnsureSingleUseOtlpExporterRegistration` via singleton `UseOtlpExporterRegistration` | covered |
| `UseOtlpExporter()` + `AddOtlpExporter()` conflicts throw `NotSupportedException` | `UseOtlpExporterWith{AddOtlpExporter*}Test` (three tests) | Same guard | covered |

### 2.3 `BindConfigurationToOptions` - `IConfiguration` binding (Issue 4 target)

`BindConfigurationToOptions` at `OtlpExporterBuilder.cs:120-163` is only
invoked when a non-null `IConfiguration` is passed to the `OtlpExporterBuilder`
constructor (line 27). It calls `services.Configure<OtlpExporterBuilderOptions>(
name, configuration)` and three companion `services.Configure<T>` calls. The
`services.Configure<T>(IConfiguration)` overload routes to
`ConfigurationBinder.Bind()` internally, which uses reflection and emits
IL2026/IL3050 in AOT-compiled apps.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `DefaultOptions` section bound from `IConfiguration["DefaultOptions:*"]` | `UseOtlpExporterConfigurationTest` | Endpoint parsed from `DefaultOptions:Endpoint` section | covered |
| `LoggingOptions` section bound from `IConfiguration["LoggingOptions:*"]` | `UseOtlpExporterConfigurationTest` | Endpoint and `ExportProcessorType` parsed | covered |
| `MetricsOptions` section bound from `IConfiguration["MetricsOptions:*"]` | `UseOtlpExporterConfigurationTest` | Endpoint and nested `PeriodicExportingMetricReaderOptions` parsed | covered |
| `TracingOptions` section bound from `IConfiguration["TracingOptions:*"]` | `UseOtlpExporterConfigurationTest` | Endpoint and `BatchExportProcessorOptions` parsed | covered |
| `LogRecordExportProcessorOptions` bound from the `LoggingOptions` sub-section | `UseOtlpExporterConfigurationTest` (asserts `ExportProcessorType.Simple`, `ScheduledDelayMilliseconds == 1000`) | `configuration.GetSection("LoggingOptions")` passed to `services.Configure<LogRecordExportProcessorOptions>` | covered |
| `MetricReaderOptions` bound from the `MetricsOptions` sub-section | `UseOtlpExporterConfigurationTest` (asserts `TemporalityPreference.Delta`, `ExportIntervalMilliseconds == 1001`) | `configuration.GetSection("MetricsOptions")` | covered |
| `ActivityExportProcessorOptions` bound from the `TracingOptions` sub-section | `UseOtlpExporterConfigurationTest` (asserts `ExportProcessorType.Simple`, `ScheduledDelayMilliseconds == 1002`) | `configuration.GetSection("TracingOptions")` | covered |
| Unknown key in `IConfiguration` section is silently ignored (no validation) | -- | `ConfigurationBinder.Bind()` ignores unknown keys by default | missing |
| Malformed value in `IConfiguration` section (e.g. non-URI endpoint) | -- | Behaviour depends on `ConfigurationBinder.Bind()` for the `OtlpExporterOptions.Endpoint` setter; setter throws on null, accepts any `Uri` | missing (silent-accept pin for Issue 1) |
| `BindConfigurationToOptions` called in AOT-compiled app emits IL2026/IL3050 | -- | No suppression annotation; AOT warning present at publish time | missing (see Issue 4; AOT-compat test app coverage noted separately) |
| `BindConfigurationToOptions` not called when `IConfiguration` is null (no-args `UseOtlpExporter()`) | `UseOtlpExporterDefaultTest` (does not pass config; no `BindConfigurationToOptions` path) | Constructor short-circuits at `OtlpExporterBuilder.cs:27` | covered (path; AOT safety is the delta) |

### 2.4 `Configure*` delegates - cascade wiring

The four `Configure*` methods on `OtlpExporterBuilder` are the programmatic
configuration path. They register `services.Configure<OtlpExporterBuilderOptions>(name,
...)` delegates that route to the appropriate `IOtlpExporterOptions`
instance.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `ConfigureDefaultExporterOptions` sets `DefaultOptions.Endpoint` | `UseOtlpExporterConfigureTest` (`VerifyOptionsApplied` asserts `"http://default_endpoint/"`) | Delegate invoked by `IOptionsMonitor` at resolution time | covered |
| `ConfigureLoggingExporterOptions` sets `LoggingOptions.Endpoint` | `UseOtlpExporterConfigureTest` | Same mechanism | covered |
| `ConfigureMetricsExporterOptions` sets `MetricsOptions.Endpoint` | `UseOtlpExporterConfigureTest` | Same | covered |
| `ConfigureTracingExporterOptions` sets `TracingOptions.Endpoint` | `UseOtlpExporterConfigureTest` | Same | covered |
| `ConfigureLoggingProcessorOptions` sets `LogRecordExportProcessorOptions` fields | `UseOtlpExporterConfigureTest` (asserts `ExportProcessorType.Simple`, delay) | Routes to `services.Configure<LogRecordExportProcessorOptions>(name, ...)` at `OtlpExporterBuilder.cs:76` | covered |
| `ConfigureMetricsReaderOptions` sets `MetricReaderOptions` fields | `UseOtlpExporterConfigureTest` (asserts `TemporalityPreference.Delta`, interval) | Routes to `services.Configure<MetricReaderOptions>(name, ...)` at `OtlpExporterBuilder.cs:96` | covered |
| `ConfigureTracingProcessorOptions` sets `ActivityExportProcessorOptions` fields | `UseOtlpExporterConfigureTest` (asserts `ExportProcessorType.Simple`, delay) | Routes to `services.Configure<ActivityExportProcessorOptions>(name, ...)` at `OtlpExporterBuilder.cs:116` | covered |
| `Configure*` delegate beats env var: programmatic endpoint beats `OTEL_EXPORTER_OTLP_ENDPOINT` | -- | Env var is read in the `OtlpExporterOptions` constructor; `Configure<T>` delegate applied after by MEO pipeline; delegate wins | missing (priority order not pinned by test) |
| `Configure*` delegate beats `IConfiguration` binding when both are applied | -- | Both `BindConfigurationToOptions` and `ConfigureDefaultExporterOptions` can be registered; MEO applies in registration order; `Configure<T>` delegate registered after `BindConfigurationToOptions` | missing (interaction not verified) |
| Null delegate passed to `ConfigureDefaultExporterOptions` throws `ArgumentNullException` | -- | `Guard.ThrowIfNull(configure)` at `OtlpExporterBuilder.cs:52` | missing (guard not directly tested) |
| Named `Configure*` routes to the correct named `IOptionsMonitor` slot | `UseOtlpExporterConfigureTest` (Theory; `name == "testNamedOptions"` exercises the named branch) | Same `services.Configure<OtlpExporterBuilderOptions>(this.name, ...)` with correct name | covered |

### 2.5 `ApplyDefaults` cascade

At pipeline build time each signal-specific `OtlpExporterOptions` instance
has `ApplyDefaults(DefaultOptionsInstance)` called on it before being
passed to the exporter build helpers. `ApplyDefaults` (defined on
`OtlpExporterOptions`) fills only properties where `HasData == false` on the
signal instance.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Signal instance inherits `DefaultOptions.Endpoint` when no signal-specific endpoint is set | `UseOtlpExporterRespectsSpecEnvVarsTest` (default env var set; signal instances assert the fallback) | `ApplyDefaults` copies the field when signal `HasData == false` | partial (verified at options level; not verified at the built exporter consumer) |
| Signal instance retains its own endpoint when a signal-specific env var is set | `UseOtlpExporterRespectsSpecEnvVarsTest` | `HasData == true` on signal instance; `ApplyDefaults` skips | covered |
| `ConfigureDefaultExporterOptions` value cascades to a signal instance that has no programmatic override | -- | `ApplyDefaults` merges; but no test isolates "default-only set, signal unset" through a Configure delegate | missing |
| Signal env var wins over `ConfigureDefaultExporterOptions` (signal instance `HasData == true` before cascade) | -- | Env var written in constructor (`HasData` becomes true); `Configure<T>` for Default runs after; `ApplyDefaults` skips the already-set property on signal | missing (non-obvious ordering; risk of silent regression) |
| `ApplyDefaults` cascade observed at consumer (actual built exporter endpoint equals cascaded value) | -- | No end-to-end test resolves the built exporter and checks the effective endpoint | missing |
| `ApplyDefaults` called even when `DefaultOptionsInstance.HasData == false` | `UseOtlpExporterDefaultTest` (all `HasData == false`; providers built successfully) | No crash; no-op cascade | covered (implicit) |

### 2.6 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All four `OtlpExporterOptions` instances at their defaults with no env vars or config | `UseOtlpExporterDefaultTest` (asserts `DefaultOptions.Endpoint`, `DefaultOptions.Protocol`, `HasData == false` on all four) | Partial property coverage; `SdkLimitOptions`, `ExperimentalOptions`, and companion options not asserted | partial |
| Stable snapshot of `OtlpExporterBuilderOptions` entire shape including all five companion fields | -- | Not snapshotted | missing (candidate for snapshot-library pilot; complex shape due to nested options) |

### 2.7 Invalid-input characterisation (guards Issue 1)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `UseOtlpExporter(protocol, null)` throws `ArgumentNullException` | `UseOtlpExporterSetEndpointAndProtocolTest` (inline assert) | `Guard.ThrowIfNull(baseUrl)` at `OpenTelemetryBuilderOtlpExporterExtensions.cs:59` | covered |
| Null configure delegate passed to `UseOtlpExporter(Action<OtlpExporterBuilder>)` | -- | `Guard.ThrowIfNull(configure)` at `OpenTelemetryBuilderOtlpExporterExtensions.cs:82` | missing |
| Null configure delegate passed to `ConfigureDefaultExporterOptions` throws | -- | `Guard.ThrowIfNull(configure)` at `OtlpExporterBuilder.cs:52` | missing (guard not tested) |
| Malformed endpoint in `IConfiguration` binding (e.g. not a valid URI string) | -- | `OtlpExporterOptions.Endpoint` setter throws on non-`Uri`; `ConfigurationBinder.Bind()` would use the property setter, triggering this exception at options-resolution time | missing (silent or exception at DI build; behaviour not pinned) |
| `ActivityExportProcessorOptions` null at factory time (signal not enabled) causes `NullReferenceException` on `this.ActivityExportProcessorOptions!.BatchExportProcessorOptions` at `OtlpExporterBuilderOptions.cs:44` | -- | `!` suppresses nullable warning; actual null would throw; the factory uses `?.Get(name)` defensively but the constructor is not defensive | missing (latent crash; see Notes) |

### 2.8 Reload no-op baseline

`OtlpExporterBuilderOptions` does not participate in reload. The factory
is invoked once per `IOptionsMonitor.Get(name)` call; the result is cached
by `IOptionsMonitor`. Changes to `IConfiguration` after provider build do
not reach the built exporters.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<OtlpExporterBuilderOptions>.OnChange` on `IConfigurationRoot.Reload()` -> built exporter endpoint unchanged | -- | Not verified | missing |
| `IOptionsMonitor<OtlpExporterBuilderOptions>.CurrentValue` returns new instance after `IConfigurationRoot.Reload()` (options-layer change fires) but pipeline ignores it | -- | Not verified | missing |

Reload no-op rows are expected to flip under Issue 17 and Issue 23. Shared
pathway specification: see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

---

## 3. Recommendations

One recommendation per gap or set of tightly related gaps. Test names
follow the dominant `Subject_Condition_Expected` convention from the
Session 0a naming survey. Target locations are existing files; new files
only where noted. Tier mapping per entry-doc Section 3.

### 3.1 Constructor and factory behaviour

1. **`OtlpExporterBuilderOptions_SdkLimitOptions_ResolvedAsCurrentValue_NotNamed`**
   (new test in `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI - resolve both
     `IOptionsMonitor<SdkLimitOptions>.CurrentValue` and
     `IOptionsMonitor<OtlpExporterBuilderOptions>.CurrentValue.SdkLimitOptions`
     and assert they are the same instance (or equal value if struct-like).
     Confirms the factory comment at `OtlpExporterBuilder.cs:178` is
     implemented correctly.
   - Guards Issues 1, 4, 14.
   - Code-comment hint: "BASELINE: pins that `SdkLimitOptions` in the
     builder uses the unnamed/default slot. If Issue 14 adds per-signal
     limit options, this test must be updated intentionally."
   - Risk vs reward: low brittleness; closes the only gap in factory
     field-wiring coverage.

2. **`OtlpExporterBuilderOptions_ExperimentalOptions_ResolvedByName`**
   (new; same file).
   - Tier 2. Mechanism: DI - configure a named `ExperimentalOptions`
     instance, then assert it is reflected in the built
     `OtlpExporterBuilderOptions`.
   - Guards Issue 4 (confirms named-slot resolution path works correctly).
   - Code-comment hint: "BASELINE: pins named-slot resolution for
     `ExperimentalOptions`. Expected to remain under Issue 4."
   - Risk vs reward: low effort; ensures the named vs unnamed distinction
     for companion options is covered.

### 3.2 `BindConfigurationToOptions` - AOT binding coverage (Issue 4)

3. **`UseOtlpExporter_Configuration_BindsAllFourSections_AOTCompatibilityCovered`**
   (audit note, not a new test here).
   - The `OpenTelemetry.AotCompatibility.TestApp` should exercise the
     `UseOtlpExporter(IConfiguration)` overload to expose the IL2026/IL3050
     warning. The per-pathway AOT file
     ([`../pathways/aot-binding.md`](../pathways/aot-binding.md)) owns this
     recommendation; cited here because `BindConfigurationToOptions` is the
     unique AOT risk surface in `OtlpExporterBuilderOptions`.
   - Guards Issue 4.

4. **`UseOtlpExporter_Configuration_UnknownKey_IsSilentlyIgnored`** (new;
   `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI - build an `IConfiguration` with an unknown key
     (e.g. `DefaultOptions:NonExistentProperty`) and assert the build
     succeeds without exception and the known properties are unaffected.
   - Guards Issue 1 (validation), Issue 4 (AOT - confirms the silent-ignore
     behaviour is not changed by the reflection-to-constructor migration).
   - Code-comment hint: "BASELINE: pins silent ignore of unknown
     configuration keys. Expected to change under Issue 1 if strict-binding
     mode is adopted."
   - Risk vs reward: low effort; important safety-net for the Issue 4
     migration.

5. **`UseOtlpExporter_Configuration_MalformedEndpoint_ThrowsAtResolutionTime`**
   (new; same file).
   - Tier 2. Mechanism: DI + Exception - pass a non-URI string for
     `DefaultOptions:Endpoint` and assert `InvalidOperationException` or
     `FormatException` is thrown when the options are resolved (the
     `OtlpExporterOptions.Endpoint` setter will be invoked by the binder).
   - Guards Issues 1, 4.
   - Code-comment hint: "BASELINE: pins exception-at-resolution behaviour
     for malformed endpoint in `IConfiguration`. If Issue 1 adds
     `ValidateOnStart`, this exception moves to startup."
   - Risk vs reward: moderate (exception type may vary by TFM); high value
     because it pins the consumer-visible failure mode for the most common
     misconfiguration.

### 3.3 `Configure*` delegate priority order

6. **`ConfigureDefaultExporterOptions_BeatsEnvVar_ForDefaultOptionsEndpoint`**
   (new; `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI + env-var fixture (`[Collection("EnvVars")]`
     already present). Set `OTEL_EXPORTER_OTLP_ENDPOINT`, then call
     `UseOtlpExporter(builder => builder.ConfigureDefaultExporterOptions(
     o => o.Endpoint = X))`. Assert `DefaultOptions.Endpoint == X`.
   - Guards Issues 1, 14.
   - Code-comment hint: "BASELINE: pins Configure<T> > env var order for
     `DefaultOptions`. Env var is read in the `OtlpExporterOptions`
     constructor; the `Configure<T>` delegate runs after; delegate wins."
   - Risk vs reward: moderate setup; high value - the priority order for
     the builder layer is not currently tested.

7. **`ConfigureDefaultExporterOptions_BeatsIConfiguration_WhenBothRegistered`**
   (new; same file).
   - Tier 2. Mechanism: DI - pass `IConfiguration` with
     `DefaultOptions:Endpoint == A` and also call
     `ConfigureDefaultExporterOptions(o => o.Endpoint = B)`. Assert `B` wins.
   - Guards Issues 1, 4 (pins that the post-AOT-fix constructor read +
     Configure<T> ordering still holds).
   - Code-comment hint: "BASELINE: pins Configure<T> > IConfiguration for
     `DefaultOptions.Endpoint`. Registration order matters: `BindConfigurationToOptions`
     registers before the delegate; MEO applies in registration order."
   - Risk vs reward: moderate; critical for verifying Issue 4 fix does not
     silently invert priority.

### 3.4 `ApplyDefaults` cascade (guards Issues 1 and 14)

8. **`UseOtlpExporter_DefaultOptionsEndpoint_CascadesToSignalInstances_WhenSignalEnvVarAbsent`**
   (new; `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: DI `IOptionsMonitor<OtlpExporterBuilderOptions>`.
     Call `ConfigureDefaultExporterOptions(o => o.Endpoint = X)` with no
     signal-specific env vars or delegates. Assert `LoggingOptions.Endpoint`,
     `MetricsOptions.Endpoint`, and `TracingOptions.Endpoint` all equal `X`
     after `ApplyDefaults` runs (which happens at pipeline build time; resolve
     through a built provider or use `GetBuilderOptionsAndValidateRegistrations`
     pattern if accessible).
   - Note: `ApplyDefaults` is called inside the `ConfigureOpenTelemetry*`
     callbacks, not at options-resolution time. The direct
     `IOptionsMonitor<OtlpExporterBuilderOptions>.Get(name)` call returns the
     pre-cascade instance. Verifying post-cascade values requires building
     the full provider pipeline. A mock exporter (see entry-doc Section 2.D)
     or `InternalsVisibleTo`-enabled internal accessor on the built exporter
     is the least-brittle approach.
   - Guards Issues 1, 14.
   - Code-comment hint: "BASELINE: pins `ApplyDefaults` cascade from
     Default to signal instances."
   - Risk vs reward: moderately complex setup due to the provider-build
     requirement; high value because the cascade is the most architecturally
     significant behaviour of this class.

9. **`UseOtlpExporter_SignalEnvVar_WinsOverDefaultConfigureDelegate`** (new;
   same file).
   - Tier 2. Mechanism: DI + env-var fixture. Set
     `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`, then call
     `ConfigureDefaultExporterOptions(o => o.Endpoint = Y)`. After provider
     build, verify the tracing exporter uses the env-var value (not `Y`).
   - Guards Issues 1, 14.
   - Code-comment hint: "BASELINE: pins that signal-specific env var wins
     over a Default Configure delegate because the signal instance
     `HasData == true` before `ApplyDefaults` runs. Non-obvious from the
     source."
   - Risk vs reward: high value; non-obvious ordering behaviour; moderate
     setup (provider build + env var isolation).

### 3.5 Invalid-input and guard coverage (guards Issue 1)

10. **`OtlpExporterBuilder_ConfigureDefaultExporterOptions_NullDelegate_Throws`**
    (new; `UseOtlpExporterExtensionTests.cs` or a new
    `OtlpExporterBuilderTests.cs` file if the group grows).
    - Tier 1. Mechanism: Exception. Construct an `OtlpExporterBuilder`
      via `UseOtlpExporter` and immediately call
      `builder.ConfigureDefaultExporterOptions(null!)`. Assert
      `ArgumentNullException`.
    - Guards Issue 1.
    - Code-comment hint: "BASELINE: pins `Guard.ThrowIfNull` at
      `OtlpExporterBuilder.cs:52`. Stable."
    - Risk vs reward: low effort; closes the null-guard gap.

11. **`UseOtlpExporter_ActionConfigure_NullDelegate_Throws`** (new; same
    file).
    - Tier 1. Mechanism: Exception. Call `builder.UseOtlpExporter(
      (Action<OtlpExporterBuilder>)null!)`. Assert `ArgumentNullException`.
    - Guards Issue 1.
    - Risk vs reward: low effort; covers the one unverified entry-point guard.

12. **`OtlpExporterBuilderOptions_ActivityExportProcessorOptions_Null_ThrowsAtConstruction`**
    (new; `UseOtlpExporterExtensionTests.cs`).
    - Tier 2. Mechanism: Exception. This scenario requires constructing the
      factory without the tracing signal enabled, so
      `ActivityExportProcessorOptions` is null. The non-null-asserting `!`
      at `OtlpExporterBuilderOptions.cs:44` would then throw a
      `NullReferenceException`. The test confirms current behaviour and
      pins it for Issue 14 (component factory) which may make the null
      path safe.
    - Guards Issues 1, 14.
    - Code-comment hint: "BASELINE: pins crash when
      `ActivityExportProcessorOptions` is null (signal not enabled). Expected
      to be made safe under Issue 14 or when per-signal enable/disable
      lands."
    - Risk vs reward: moderate (requires constructing the DI container
      without tracing enabled while still triggering the factory); high
      value as a latent crash pin.

### 3.6 Reload no-op baseline

13. **`UseOtlpExporter_ReloadOfConfiguration_DoesNotChangeBuiltExporterEndpoint`**
    (new; `UseOtlpExporterExtensionTests.cs`).
    - Tier 2. Mechanism: DI + mock exporter or `InternalsVisibleTo` accessor
      on the built provider's exporter endpoint. Trigger
      `IConfigurationRoot.Reload()` after provider build; assert the built
      exporter's effective endpoint is unchanged.
    - Guards Issues 17, 23.
    - Code-comment hint: "BASELINE: pins no-op reload for the
      `UseOtlpExporter` pipeline. Expected to flip under Issue 23
      (`ReloadableOtlpExportClient`)."
    - Risk vs reward: see shared spec in
      [`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).
      Moderate effort; high value without which Issue 23 has no test delta.

### Prerequisites and dependencies

- 3.3 and 3.4 depend on the env-var isolation pattern decision
  (entry-doc Section 5); the `[Collection("EnvVars")]` pattern already
  present in `UseOtlpExporterExtensionTests.cs` covers Tier 2 tests.
- 3.4 (recommendations 8 and 9) depend on a mechanism for observing
  post-cascade values at the consumer. A mock exporter approach (DI
  Section 2.D in entry doc) is recommended over reflection on the built
  provider. The AOT pathway file should confirm the mock approach is
  AOT-safe.
- 3.6 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  landing first so the test can follow a shared template.
- Recommendation 3 (AOT audit) depends on the AOT pathway file
  ([`../pathways/aot-binding.md`](../pathways/aot-binding.md)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1, 3.3, 3.4, 3.5.
- **Issue 4** - Fix AOT bug: reflection-based binding in
  `OtlpExporterBuilder.cs`. Guarded by: Section 3.2 (AOT-binding scenarios;
  `BindConfigurationToOptions` at `OtlpExporterBuilder.cs:120-163` is the
  sole IL2026/IL3050 source in this class). Section 3.3 (priority-order
  tests pin the post-fix behaviour).
- **Issue 14** - Register OTLP exporter component factories. Guarded by:
  Sections 3.1 (companion-options wiring), 3.4 (cascade to consumer),
  3.5.3 (null companion options latent crash).
- **Issue 17** - Design and implement standard `OnChange` subscriber
  pattern. Guarded by: Section 3.6.
- **Issue 23** - OTLP exporter reload: `ReloadableOtlpExportClient` and
  `IOptionsMonitor`-aware constructor. Guarded by: Section 3.6.
