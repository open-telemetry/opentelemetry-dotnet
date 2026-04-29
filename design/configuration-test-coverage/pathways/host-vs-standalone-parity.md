# Host vs Standalone Parity - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/OpenTelemetry.Extensions.Hosting/OpenTelemetryServicesExtensions.cs:39-49` - `AddOpenTelemetry`
- `src/OpenTelemetry.Extensions.Hosting/OpenTelemetryBuilder.cs:68-110` - `WithMetrics`, `WithTracing`, `WithLogging`
- `src/OpenTelemetry/Trace/Builder/TracerProviderBuilderBase.cs:40-54` - DI/hosted `TryAddSingleton` path
- `src/OpenTelemetry/Trace/Builder/TracerProviderBuilderBase.cs:143-170` - standalone `Build()` path
- `src/OpenTelemetry/Sdk.cs:63-103` - CreateTracerProviderBuilder, CreateMeterProviderBuilder,
  CreateLoggerProviderBuilder
- `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryServicesExtensionsTests.cs`
  - `AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest`
  - `AddOpenTelemetry_WithMetrics_HostConfigurationHonoredTest`
  - `AddOpenTelemetry_WithLogging_HostConfigurationHonoredTest`
- `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderExtensionsTests.cs`
  - `ConfigureBuilderIConfigurationAvailableTest`
  - `ConfigureBuilderIConfigurationModifiableTest`
- `test/OpenTelemetry.Tests/Metrics/MeterProviderBuilderExtensionsTests.cs`
  - `ConfigureBuilderIConfigurationAvailableTest`
  - `ConfigureBuilderIConfigurationModifiableTest`
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/UseOtlpExporterExtensionTests.cs`
  - `UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest`

**Three initialization paths:**

1. **Hosted path** (`IHost`): `services.AddOpenTelemetry().WithTracing()/.WithMetrics()/.WithLogging()`.
   Providers are registered as singletons in the host's DI container and resolved lazily by
   `TelemetryHostedService`. `IConfiguration` is the host's full configuration hierarchy (env vars,
   appsettings.json, etc.). SDK reads configuration from whichever `IConfiguration` is registered in DI.

2. **Standalone builder path**: `Sdk.CreateTracerProviderBuilder().Build()` (and equivalents). Calls
   `TracerProviderBuilderBase.Build()` which constructs its own `IServiceProvider`. That provider's
   `IConfiguration` is populated by the default SDK registration, which sources env vars only.
   Custom `IConfiguration` can be injected via `ConfigureServices(s => s.AddSingleton<IConfiguration>(...))`
   before `Build()`.

3. **Non-host DI path** (bare `IServiceCollection` without `IHost`): `services.AddOpenTelemetry()...`
   resolved directly from a manually built `IServiceProvider`. `IConfiguration` behaviour depends on
   what the caller registers; there is no automatic env-var sourcing unless the caller adds it or uses
   `Microsoft.Extensions.Configuration.EnvironmentVariables`.

**Priority hierarchy difference:** In the hosted path the host provides a fully composed `IConfiguration`
(env vars + appsettings + overrides) automatically. In the standalone path only env vars are available
by default. In the non-host DI path neither source is automatic. The env-var-to-IConfiguration priority
order is consistent within each path but the set of sources available differs across paths.

## 1. Existing coverage

<!-- markdownlint-disable MD013 -->
| File:method | Scenario summary | Observation mechanism | Env-var isolation status |
| --- | --- | --- | --- |
| `AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest` | Hosted: deferred `ConfigureBuilder` callback receives host `IConfiguration` | DI (callback invocation verified) | None (no env var set) |
| `AddOpenTelemetry_WithMetrics_HostConfigurationHonoredTest` | Same for metrics | DI | None |
| `AddOpenTelemetry_WithLogging_HostConfigurationHonoredTest` | Same for logging | DI | None |
| `TracerProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationAvailableTest` | Standalone: `IConfiguration` auto-available in `ConfigureBuilder` | DI (callback invocation) | `EnvironmentVariableScope` |
| `TracerProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationModifiableTest` | Standalone: custom `IConfiguration` via `ConfigureServices` | DI | None |
| `MeterProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationAvailableTest` | Same for meters | DI | `EnvironmentVariableScope` |
| `MeterProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationModifiableTest` | Same for meters | DI | None |
| `UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | `UseOtlpExporter` path: `IConfiguration` overrides env vars | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `IDisposable` class-level snapshot |
<!-- markdownlint-enable MD013 -->

The existing tests verify that `IConfiguration` is **available** in callbacks under each initialization
path. They do not verify that the same key-value pair in `IConfiguration` produces the same effective
options value across all three paths.

## 2. Scenario checklist and gap analysis

### 2.1 IConfiguration availability

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Hosted: `IConfiguration` available in `ConfigureBuilder` callback (tracing) | `AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest` | Callback receives host IConfiguration | Covered |
| Hosted: `IConfiguration` available in `ConfigureBuilder` callback (metrics) | `AddOpenTelemetry_WithMetrics_HostConfigurationHonoredTest` | Callback receives host IConfiguration | Covered |
| Hosted: `IConfiguration` available in `ConfigureBuilder` callback (logging) | `AddOpenTelemetry_WithLogging_HostConfigurationHonoredTest` | Callback receives host IConfiguration | Covered |
| Standalone: `IConfiguration` available in `ConfigureBuilder` callback (tracing) | `TracerProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationAvailableTest` | IConfiguration present | Covered |
| Standalone: `IConfiguration` available in `ConfigureBuilder` callback (metrics) | `MeterProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationAvailableTest` | IConfiguration present | Covered |
| Standalone: `IConfiguration` available in `ConfigureBuilder` callback (logging) | None identified | IConfiguration present | Missing |
<!-- markdownlint-enable MD013 -->

### 2.2 Cross-path options value parity

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Same `OTEL_BSP_SCHEDULE_DELAY` value produces identical `ScheduledDelayMilliseconds` in hosted vs standalone | None | Same options value expected | Missing |
| Same `OTEL_EXPORTER_OTLP_ENDPOINT` env var produces identical effective endpoint in hosted vs standalone | None | Same effective endpoint expected | Missing |
| `IConfiguration` key injected via `AddInMemoryCollection` in standalone equals what hosted path reads from the same source | None | Same resolved value expected | Missing |
| Non-host DI path: env vars not automatically sourced unless `AddEnvironmentVariables()` is called | None | Silent difference from hosted behaviour | Missing |
<!-- markdownlint-enable MD013 -->

### 2.3 OTLP env-var precedence across paths

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_EXPORTER_OTLP_*` env vars honoured in hosted `UseOtlpExporter` path | `UseOtlpExporterRespectsSpecEnvVarsTest` | Env vars applied via options constructor | Covered |
| `IConfiguration` values override env vars in `UseOtlpExporter` hosted path | `UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | IConfiguration overrides env var defaults | Covered |
| `IConfiguration` values override env vars in `UseOtlpExporter` standalone path | None | Same override expected but untested | Missing |
<!-- markdownlint-enable MD013 -->

## 3. Recommendations

### 3.1 `LoggerProviderBuilder_Standalone_IConfigurationAvailableInConfigureBuilder`

**Target test name:** `LoggerProviderBuilder_Standalone_IConfigurationAvailableInConfigureBuilder`
**Location:** A new `LoggerProviderBuilderExtensionsTests.cs` in `test/OpenTelemetry.Tests/Logs/`,
or an existing test file for logger-provider builder extensions if one exists.
**Tier:** 1
**Observation mechanism:** DI - verify that the `IConfiguration` passed to the `ConfigureBuilder`
callback is not null and contains the expected key. Pattern is identical to
`TracerProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationAvailableTest`.
**Guards issues:** indirect guard for Issues 15 and 16 (declarative config must wire IConfiguration
into the logging path the same way as tracing and metrics).

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - IConfiguration callback parameter is non-null and carries expected key.
// Coverage index: pathway.host-vs-standalone-parity.iconfiguration-available.standalone-logging
```

### 3.2 `BatchProcessorOptions_HostedVsStandalone_SameEffectiveValue`

**Target test name:** `BatchProcessorOptions_HostedVsStandalone_SameEffectiveValue`
**Location:** `test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`
**Tier:** 2
**What to test:** Set `OTEL_BSP_SCHEDULE_DELAY` to a non-default value via `EnvironmentVariableScope`.
Build one `TracerProvider` via `Sdk.CreateTracerProviderBuilder().Build()` (standalone) and one via
`services.AddOpenTelemetry().WithTracing()` (non-host DI). Assert that `ScheduledDelayMilliseconds`
resolves to the same value in both.
**Observation mechanism:** `DirectProperty` - read `BatchExportActivityProcessorOptions.ScheduledDelayMilliseconds`
after construction from each path.
**Guards issues:** Issues 15 and 16 - if the declarative-config IConfigurationProvider is injected
into one path but not the other, the parity contract breaks.
**Risks pinned:** the standalone path builds its own IServiceProvider; if a declarative-config
IConfigurationProvider is wired only into the hosted path, env vars would still be read but YAML
values would not propagate to the standalone path.

```csharp
// BASELINE: pins that hosted and standalone paths resolve the same ScheduledDelayMilliseconds value.
// Expected to change under Issue #15 (declarative config - verify parity is maintained).
// Guards risks: Risk 1.4 (parameterless constructors bypass declarative config).
// Observation: DirectProperty - low brittleness.
// Coverage index: pathway.host-vs-standalone-parity.batch-processor-options.cross-path-parity
```

**Risk vs reward:** Medium effort (two provider builds plus env-var isolation). High value: the three
initialization paths have different IConfiguration assembly; a refactor that wires declarative config
into only one path would be caught here.

### 3.3 `OtlpExporter_UseOtlpExporter_Standalone_IConfigurationOverridesEnvVar`

**Target test name:** `OtlpExporter_UseOtlpExporter_Standalone_IConfigurationOverridesEnvVar`
**Location:** `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/UseOtlpExporterExtensionTests.cs`
**Tier:** 2
**What to test:** Inject a custom `IConfiguration` (via `services.AddSingleton<IConfiguration>(...)`)
into the standalone `UseOtlpExporter` path. Assert that the IConfiguration-supplied endpoint overrides
the env var value - the same behaviour already verified for the hosted path by
`UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest`.
**Observation mechanism:** DI `IOptionsMonitor<OtlpExporterBuilderOptions>.Get(name)`.
**Guards issues:** Issues 15 and 16.

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI IOptionsMonitor<OtlpExporterBuilderOptions> - low brittleness.
// Coverage index: pathway.host-vs-standalone-parity.otlp-exporter.standalone-iconfiguration-override
```

### 3.4 `NonHostDI_WithoutEnvironmentVariables_EnvVarNotAutoSourced`

**Target test name:** `NonHostDI_WithoutEnvironmentVariables_EnvVarNotAutoSourced`
**Location:** `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderExtensionsTests.cs` (or a new
host-vs-standalone parity test file if the maintainer prefers separation).
**Tier:** 2
**What to test:** Build a `TracerProvider` via a bare `IServiceCollection.AddOpenTelemetry().WithTracing()`
without registering an `IConfiguration` that sources env vars. Set an `OTEL_BSP_*` env var via
`EnvironmentVariableScope`. Assert that the resulting `BatchExportActivityProcessorOptions` does NOT
reflect the env var value (i.e., it uses the default). This documents the documented gap: non-host DI
does not automatically source env vars.
**Observation mechanism:** `DirectProperty`.
**Guards issues:** Issues 15 and 16.
**Risks pinned:** this silent difference from the hosted path is likely to cause confusion when
declarative config (YAML IConfigurationProvider) is introduced; a test that pins today's behaviour
makes the Issue 15 delta visible.

```csharp
// BASELINE: pins current behaviour (non-host DI does not auto-source env vars).
// Expected to change under Issue #15 if declarative config adds an automatic env-var IConfigurationProvider
// to the non-host path.
// Observation: DirectProperty - low brittleness.
// Coverage index: pathway.host-vs-standalone-parity.env-var-sourcing.non-host-di-not-automatic
```

## Guards issues

- Issue 15 (declarative config - YAML `IConfigurationProvider`): the YAML provider must work in all
  three initialization paths. Tests 3.2, 3.3, and 3.4 pin the pre-15 parity baseline; divergence after
  Issue 15 lands should be explicit and deliberate.
- Issue 16 (tree walker / component registry): same requirement - component resolution from YAML config
  must behave consistently across hosted, standalone, and non-host DI paths.
