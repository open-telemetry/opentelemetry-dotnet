# Provider Global Switches - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/OpenTelemetry/SdkConfigDefinitions.cs:8` - `SdkDisableEnvVarName` constant (`"OTEL_SDK_DISABLED"`)
- `src/OpenTelemetry/Trace/Builder/TracerProviderBuilderBase.cs:44-54` - hosted DI read (TryAddSingleton path)
- `src/OpenTelemetry/Trace/Builder/TracerProviderBuilderBase.cs:163-181` - standalone Build() read
- `src/OpenTelemetry/Metrics/Builder/MeterProviderBuilderBase.cs:133-142` - both paths (meter)
- `src/OpenTelemetry/Logs/Builder/LoggerProviderBuilderBase.cs:116-126` - both paths (logger)
- `src/OpenTelemetry/Metrics/MeterProviderSdk.cs:16` - `ExemplarFilterConfigKey` constant
- `src/OpenTelemetry/Metrics/MeterProviderSdk.cs:476-541` - `ApplySpecificationConfigurationKeys` implementation
- `src/Shared/Configuration/OpenTelemetryConfigurationExtensions.cs:69-85` - `TryGetBoolValue` implementation

**Build-time vs runtime:** Both switches are read at provider build time only. The SDK changelog
confirms that `OTEL_SDK_DISABLED` is evaluated at application startup; later changes have no effect.
`OTEL_METRICS_EXEMPLAR_FILTER` is read during `MeterProviderSdk` construction at line 58 after DI
configuration is applied.

**Parsing semantics - `OTEL_SDK_DISABLED`:** Uses `bool.TryParse()` (case-insensitive: "true", "True",
"TRUE" all disable the SDK). An unrecognised value causes `InvalidConfigurationValue` Event 47 at Warning
level and leaves the SDK **enabled**. An explicit "false" leaves the SDK enabled. A missing key leaves
the SDK enabled. The SDK emits `TracerProviderSdkEvent` / `MeterProviderSdkEvent` / `LoggerProviderSdkEvent`
at Verbose (Events 46, 39, 49 respectively) when the provider is disabled.

**Parsing semantics - `OTEL_METRICS_EXEMPLAR_FILTER`:** Uses `string.Equals(...,
StringComparison.OrdinalIgnoreCase)` to match "always_off", "always_on", "trace_based". An invalid value
causes a Verbose `MeterProviderSdkEvent` (Event 39) and the filter is **not applied** - no Warning. A
programmatic `SetExemplarFilter()` call takes precedence over the env var: if `state.ExemplarFilter.HasValue`
is `true` at `MeterProviderSdk.cs:478`, the configuration value is ignored (another Verbose event is emitted).

## 1. Existing coverage

<!-- markdownlint-disable MD013 -->
| File:method | Scenario summary | Observation mechanism | Env-var isolation status |
| --- | --- | --- | --- |
| `TracerProviderBuilderBaseTests.TracerProviderIsExpectedType` (Theory: 3) | `OTEL_SDK_DISABLED` -> `NoopTracerProvider` vs `TracerProviderSdk` | Behavioural type check | `EnvironmentVariableScope` per-call |
| `LoggerProviderBuilderBaseTests.LoggerProviderIsExpectedType` | `OTEL_SDK_DISABLED=true` -> `NoopLoggerProvider` | Behavioural type check | `EnvironmentVariableScope` per-call |
| `MeterProviderBuilderBaseTests.LoggerProviderIsExpectedType` (sic) (Theory: 3) | `OTEL_SDK_DISABLED` -> `NoopMeterProvider` vs `MeterProviderSdk` | Behavioural type check | `EnvironmentVariableScope` per-call |
| `OpenTelemetryMetricsBuilderExtensionsTests.WhenOpenTelemetrySdkIsDisabledExceptionNotThrown` | Hosted DI path: `OTEL_SDK_DISABLED=true` does not throw | Behavioural (no-exception) | `EnvironmentVariableScope` (linked source) |
| `MetricExemplarTests.TestExemplarFilterSetFromConfiguration` (Theory: 6) | Exemplar filter via `IConfiguration`; programmatic precedence | DI `IOptionsMonitor` (inferred) | No env var set; uses `AddInMemoryCollection` |
<!-- markdownlint-enable MD013 -->

**Note:** The exact `InlineData` cases for the three Theory tests above were not read in this session.
The scenario checklist below flags which cases are likely covered and which require verification when
the test file is opened.

## 2. Scenario checklist and gap analysis

### 2.1 OTEL_SDK_DISABLED

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `"true"` -> noop tracer (standalone) | `TracerProviderIsExpectedType` (one InlineData case) | `NoopTracerProvider` returned | Covered |
| `"true"` -> noop meter (standalone) | `MeterProviderBuilderBaseTests.LoggerProviderIsExpectedType` | `NoopMeterProvider` returned | Covered |
| `"true"` -> noop logger (standalone) | `LoggerProviderBuilderBaseTests.LoggerProviderIsExpectedType` | `NoopLoggerProvider` returned | Covered |
| `"true"` -> noop via hosted path (metrics) | `WhenOpenTelemetrySdkIsDisabledExceptionNotThrown` | No exception; noop resolved | Covered |
| `"false"` (explicit) -> SDK enabled | Likely one InlineData case in each Theory; unverified | `TracerProviderSdk` / `MeterProviderSdk` returned | Partial (unverified) |
| Not set -> SDK enabled | Likely one InlineData case in each Theory; unverified | Real provider returned | Partial (unverified) |
| Invalid value (e.g. `"yes"`) -> SDK stays enabled | None | SDK enabled; Event 47 emitted at Warning | Missing |
| `"true"` -> noop via hosted path (tracing) | None | `NoopTracerProvider` resolved from DI | Missing |
| `"true"` -> noop via hosted path (logging) | None | `NoopLoggerProvider` resolved from DI | Missing |
<!-- markdownlint-enable MD013 -->

### 2.2 OTEL_METRICS_EXEMPLAR_FILTER

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `"always_off"` via `IConfiguration` | `TestExemplarFilterSetFromConfiguration` (one case) | `ExemplarFilterType.AlwaysOff` applied | Covered |
| `"always_on"` via `IConfiguration` | `TestExemplarFilterSetFromConfiguration` (one case) | `ExemplarFilterType.AlwaysOn` applied | Covered |
| `"trace_based"` via `IConfiguration` | Likely one case in the Theory; unverified | `ExemplarFilterType.TraceBased` applied | Partial (unverified) |
| Programmatic filter beats `IConfiguration` value | Likely covered in Theory; unverified | Config value ignored; Verbose event emitted | Partial (unverified) |
| `"always_off"` via env var (not `AddInMemoryCollection`) | None | `ExemplarFilterType.AlwaysOff` applied | Missing |
| Invalid value via `IConfiguration` | None identified | Filter not applied; Verbose Event 39 emitted | Missing |
| Key not set -> default filter retained | None | `ExemplarFilter` stays as programmatic value or null | Missing |
<!-- markdownlint-enable MD013 -->

## 3. Recommendations

### 3.1 `SdkDisabled_InvalidValue_SdkStaysEnabled`

**Target test name:** `SdkDisabled_InvalidValue_SdkStaysEnabled`
**Location:** `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderBaseTests.cs`
**Tier:** 1
**Observation mechanism:** Behavioural type check (`Assert.IsType<TracerProviderSdk>`). Low brittleness;
type membership is part of the public contract. Does not assert the EventSource event in this test
(that concern is owned by the observability-and-silent-failures pathway).
**Guards issues:** indirect guard for Issues 15 and 16 (provider build path must not alter invalid-value
handling when a new IConfigurationProvider is introduced).
**Risks pinned:** a refactor that changes the `OTEL_SDK_DISABLED` parser from `bool.TryParse` to a
custom parser could silently change the fallback-to-enabled contract.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: Behavioural - type check only; Event 47 assertion is in observability pathway.
// Coverage index: pathway.provider-global-switches.sdk-disabled.invalid-value
```

**Risk vs reward:** Low effort (one new `InlineData` or `Fact`). High value: "invalid value leaves SDK
enabled" is unintuitive; silent telemetry loss from a parser regression would be hard to debug in production.

### 3.2 `SdkDisabled_True_HostedTracing_NoopReturned`

**Target test name:** `SdkDisabled_True_HostedTracing_NoopReturned`
**Location:** `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryServicesExtensionsTests.cs`
**Tier:** 2
**Observation mechanism:** Behavioural type check on resolved `TracerProvider`. The pattern is established
by `AddOpenTelemetry_WithTracing_DisposalTest` which casts to `TracerProviderSdk` via `InternalsVisibleTo`.
**Guards issues:** indirect guard for Issues 15 and 16. The hosted DI read at
`TracerProviderBuilderBase.cs:46` is a separate code path from the standalone `Build()` read at line 163;
a refactor could break one without the other. This test pins the hosted path.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: Behavioural - type check on TracerProvider resolved from IServiceProvider.
// Coverage index: pathway.provider-global-switches.sdk-disabled.hosted-tracing
```

### 3.3 `SdkDisabled_True_HostedLogging_NoopReturned`

**Target test name:** `SdkDisabled_True_HostedLogging_NoopReturned`
**Location:** `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryServicesExtensionsTests.cs`
**Tier:** 2
**Observation mechanism:** Behavioural type check on resolved `LoggerProvider` / `LoggerProviderSdk`.
**Guards issues:** indirect guard for Issues 15 and 16. The logging disable read in
`LoggerProviderBuilderBase.cs:119` is separate from the tracing and metrics reads.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: Behavioural - type check on LoggerProvider resolved from IServiceProvider.
// Coverage index: pathway.provider-global-switches.sdk-disabled.hosted-logging
```

### 3.4 `ExemplarFilter_EnvVar_AllValues_Applied`

**Target test name:** `ExemplarFilter_EnvVar_AllValues_Applied` (Theory: 3 InlineData)
**Location:** `test/OpenTelemetry.Tests/Metrics/MetricExemplarTests.cs`
**Tier:** 2
**Observation mechanism:** `InternalAccessor` - read `MeterProviderSdk.ExemplarFilter` directly after
building the provider. `MeterProviderSdk` is an internal type already accessible to `OpenTelemetry.Tests`
via `InternalsVisibleTo`.
**Env-var isolation:** `EnvironmentVariableScope` wrapping the `OTEL_METRICS_EXEMPLAR_FILTER` key; the
class-level `IDisposable` snapshot/restore pattern used elsewhere in the file is preferred for consistency.
**Guards issues:** indirect guard for Issues 15 and 16. The env var flows through IConfiguration; a new
declarative-config `IConfigurationProvider` must not shadow it.
**Risks pinned:** `ApplySpecificationConfigurationKeys` runs after `state.ExemplarFilter` is applied at
`MeterProviderSdk.cs:56`; a reorder in a refactor could break programmatic-over-env-var precedence.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: InternalAccessor (MeterProviderSdk.ExemplarFilter) - compile-checked via InternalsVisibleTo.
// Coverage index: pathway.provider-global-switches.exemplar-filter.env-var-all-values
```

**Risk vs reward:** Medium effort (env var isolation required). High value: `TestExemplarFilterSetFromConfiguration`
only tests the `IConfiguration` injection path via `AddInMemoryCollection`; the actual production flow
reads from the env var through the standard `EnvironmentVariablesConfigurationProvider`.

### 3.5 `ExemplarFilter_InvalidValue_FilterNotApplied`

**Target test name:** `ExemplarFilter_InvalidValue_FilterNotApplied`
**Location:** `test/OpenTelemetry.Tests/Metrics/MetricExemplarTests.cs`
**Tier:** 2
**Observation mechanism:** `InternalAccessor` - assert `MeterProviderSdk.ExemplarFilter` is `null`
when configuration value is an unrecognised string.
**Guards issues:** indirect guard for Issues 15 and 16.
**Risks pinned:** the current Verbose-only feedback on an invalid filter value is effectively silent
to users. A future refactor might default to `AlwaysOff` or throw; without a baseline test, the
silent-fallback-to-null behaviour has no regression protection.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: InternalAccessor (MeterProviderSdk.ExemplarFilter) - asserts null on invalid input.
// Coverage index: pathway.provider-global-switches.exemplar-filter.invalid-value
```

## Guards issues

- Issue 15 (declarative config - YAML `IConfigurationProvider`): **indirect guard** - both switches are
  read from `IConfiguration`; introducing a new provider must not reorder or shadow the env-var source.
- Issue 16 (tree walker / component registry): **indirect guard** - the component registry refactor must
  not alter the assembly order in which `IConfiguration` is built before the provider is constructed.
