# Env-Var Precedence - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/Shared/Options/DelegatingOptionsFactory.cs` - `Create(string name)` at
  line 79. Execution order within one `Create` call: (1) constructor via
  `optionsFactoryFunc`, (2) `Configure<T>` delegates in registration order,
  (3) `PostConfigure<T>` delegates, (4) `IValidateOptions<T>` chain.
  Env-var reads happen in step 1, before any `Configure<T>` callback.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:50` -
  public constructor delegates to the internal three-argument overload, which
  calls `ApplyConfiguration` then `ApplyConfigurationUsingSpecificationEnvVars`.
  All `OTEL_EXPORTER_OTLP_*` reads happen during construction before the DI
  pipeline calls any `Configure<T>` delegate.
- `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:28` -
  public constructor: `this(new ConfigurationBuilder().AddEnvironmentVariables().Build())`.
  Internal constructor reads `OTEL_BSP_*` keys inline. Direct construction
  always reads the process environment regardless of DI.
- `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs:79` -
  standalone (`Sdk.Create*`) IConfiguration fallback registers
  `new ConfigurationBuilder().AddEnvironmentVariables().Build()`. Host-builder
  usage instead uses the host's full `IConfiguration`, which follows the
  standard .NET priority order: env vars added after appsettings.json layers,
  so env vars beat appsettings.json.

## 1. Existing coverage

Section 1 is facts-only; no gap marking.

| File:method | Scenario summary | Observation | Env-var isolation |
| --- | --- | --- | --- |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_EnvironmentVariableOverride` | `OTEL_BSP_*` env vars override constructor defaults | DirectProperty | Class-IDisposable |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_UsingIConfiguration` | `AddInMemoryCollection` IConfiguration binding overrides defaults | DirectProperty | Class-IDisposable |
| `BatchExportActivityProcessorOptionsTests.BatchExportProcessorOptions_SetterOverridesEnvironmentVariable` | Direct property setter applied after construction overrides env var | DirectProperty | Class-IDisposable |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | `OTEL_BLRP_*` env vars override defaults | DirectProperty | Class-IDisposable |
| `BatchExportLogRecordProcessorOptionsTests.BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable` | Direct setter overrides env var | DirectProperty | Class-IDisposable |
| `OtlpExporterOptionsTests.OtlpExporterOptions_EnvironmentVariableOverride` | `OTEL_EXPORTER_OTLP_*` env vars override defaults (Theory, all signal types) | DirectProperty | Class-IDisposable+[Collection] |
| `OtlpExporterOptionsTests.OtlpExporterOptions_UsingIConfiguration` | IConfiguration binding overrides defaults (Theory, all signal types) | DirectProperty | Class-IDisposable+[Collection] |
| `OtlpExporterOptionsTests.OtlpExporterOptions_SetterOverridesEnvironmentVariable` | Direct setter overrides env var | DirectProperty | Class-IDisposable+[Collection] |
| `OpenTelemetryLoggingExtensionsTests.UseOpenTelemetryOptionsOrderingTest` | `Configure<T>` ordering: before-bind / extension / after via DI | DI | None (no env var mutation) |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | `IOptionsMonitor` reads env-var values from DI-injected IConfiguration | DI | Class-IDisposable+[Collection] |

Note: All `*_SetterOverridesEnvironmentVariable` tests apply a direct property
assignment AFTER constructing the options object. They verify that the in-memory
object state beats the constructor-time env var read. They do NOT exercise the
DI `Configure<T>` pipeline, so they leave the factory-to-delegate ordering
unverified through the DI flow.

## 2. Scenario checklist and gap analysis

### 2.1 Configure<T> delegate vs env var (DI pipeline)

`Configure<T>` delegates run in step 2 of `DelegatingOptionsFactory.Create`,
after the constructor has already read env vars in step 1. A delegate therefore
can override any env-var-sourced value.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Direct setter after construction overrides env var | `*_SetterOverridesEnvironmentVariable` (all classes) | Setter wins | covered |
| DI `Configure<T>` delegate overrides env var - `OtlpExporterOptions` | none | `Configure<T>` runs after ctor; delegate value wins | missing |
| DI `Configure<T>` delegate overrides env var - `BatchExportActivityProcessorOptions` | none | Same ordering; delegate value wins | missing |
| Multiple `Configure<T>` registrations - last wins over all previous sources | none | Last `Configure<T>` wins; env var baked in at ctor and overwritten | missing |

`UseOpenTelemetryOptionsOrderingTest` tests delegate ordering on
`OpenTelemetryLoggerOptions`, which does not read env vars in its constructor.
It does not cover the env-var-to-Configure interaction for options classes that
do read env vars.

### 2.2 Configure<T> delegate vs appsettings.json IConfiguration binding

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Configure<T>` wins over appsettings.json when both set same key | none | `Configure<T>` runs after ctor IConfiguration read; delegate wins | missing |
| appsettings.json value applied when no `Configure<T>` override present | `*_UsingIConfiguration` (all classes) | IConfiguration value used when key present | covered |

### 2.3 appsettings.json vs env var (host-builder IConfiguration hierarchy)

In a host-builder scenario the host registers an IConfiguration that layers
env vars on top of appsettings.json (standard .NET host behavior). The SDK uses
this IConfiguration in the options constructor, so env vars beat appsettings.json
for any key present in both.

In the standalone SDK path (`Sdk.Create*`), the registered IConfiguration is
`new ConfigurationBuilder().AddEnvironmentVariables().Build()` only; no
appsettings.json layer is added.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| env var beats appsettings.json when both define same key (host builder) | none | Env var added after appsettings in host; env var wins | missing |
| appsettings.json applies when env var absent (host builder) | hosting `HostConfigurationHonoredTest` (indirect) | Host IConfiguration used; no env var means appsettings value seen | partial |
| Standalone SDK has no appsettings.json source | none | `AddEnvironmentVariables()` only; appsettings keys invisible | missing |

`AddOpenTelemetry_With*_HostConfigurationHonoredTest` verifies the host
IConfiguration is available in configure callbacks, but does not set both an env
var and an appsettings key to the same options property to assert the winner.
Coverage is therefore partial.

### 2.4 Constructor default vs all sources (full priority chain)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Constructor default when no source sets the key | `*_Defaults` tests (all classes) | Coded default applied | covered |
| env var overrides constructor default | `*_EnvironmentVariableOverride` tests | env var value wins | covered |
| IConfiguration overrides constructor default | `*_UsingIConfiguration` tests | IConfiguration value wins | covered |
| Full order (ctor-default < env var < appsettings < Configure<T>) in one test | none | Each pair tested in isolation; full chain untested | missing |

### 2.5 Process-global env var reads vs DI-scoped IConfiguration override

All current env-var reads use `IConfiguration` built from `AddEnvironmentVariables()`.
When using a host builder, a custom `IConfiguration` can be injected via
`ConfigureServices`; that custom configuration beats the env-var-only fallback
because `TryAddSingleton` skips re-registering if a registration already exists.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Env vars read at constructor time (not re-read later) | implicit in all env-var tests | Reads happen once in ctor; later env changes invisible | partial |
| DI-scoped IConfiguration override beats process env var | none | Not tested; pattern valid but unverified | missing |
| Env var set after DI build has no effect on resolved options | none | Not tested; reads happen at ctor time only | missing |

## 3. Recommendations

### R1: DI Configure<T> vs env var

- **Target test:** `OtlpExporterOptions_ConfigureT_OverridesEnvVar`
- **Location:** `OtlpExporterOptionsTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - register an `IConfiguration` with a known endpoint
  value (via `AddInMemoryCollection`) then add a `Configure<OtlpExporterOptions>`
  delegate that sets a different endpoint. Resolve `IOptions<OtlpExporterOptions>`
  and assert the delegate's endpoint wins. Avoids process env-var mutation.
- **Guards issues:** 1, 5, 15
- **Risks pinned:** 1.1, 1.2
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #15 (declarative config IConfigurationProvider
// must sit at correct priority in the chain).
// Guards risks: 1.1, 1.2.
// Observation: DI - IOptions<OtlpExporterOptions>; Configure<T> wins over env var.
// Coverage index: pathway.env-var-precedence.configure-t.beats-env-var
```

- **Risk vs reward:** Low-effort Tier 2 test. Pins the env-var-to-Configure ordering
  that Issues 1, 5, and 15 all assume. Without this test a change in factory
  execution order would be invisible until a runtime regression surfaces.

### R2: DI Configure<T> vs env var - BatchExportActivityProcessorOptions

- **Target test:** `BatchExportProcessorOptions_ConfigureT_OverridesEnvVar`
- **Location:** `BatchExportActivityProcessorOptionsTests.cs`
  (`test/OpenTelemetry.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - inject an IConfiguration with `OTEL_BSP_SCHEDULE_DELAY`
  and register a `Configure<BatchExportActivityProcessorOptions>` with a different
  `ScheduledDelayMilliseconds`. Resolve `IOptions<T>` and assert delegate wins.
- **Guards issues:** 1, 5
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #5 (PostConfigure cascade for SdkLimitOptions
// adds a new PostConfigure step; precedence order must remain stable).
// Guards risks: 1.1.
// Observation: DI - IOptions<BatchExportActivityProcessorOptions>.
// Coverage index: pathway.env-var-precedence.configure-t.beats-env-var-bsp
```

- **Risk vs reward:** Low effort. Companion to R1 for the core-SDK options class.

### R3: Full 4-level priority chain

- **Target test:**
  `BatchExportProcessorOptions_PriorityChain_ConfigureTWinsOverAllOthers`
- **Location:** `BatchExportActivityProcessorOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - layer two `AddInMemoryCollection` sources (one acting
  as appsettings, one as env-var-layer) and add a `Configure<T>` delegate. Use
  `InlineData` to vary which source sets the key and assert the winner in each
  case. No process env-var mutation needed.
- **Guards issues:** 1, 5, 15
- **Risks pinned:** 1.1, 1.2
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #15 (declarative config provider placement).
// Guards risks: 1.1, 1.2.
// Observation: DI - layered IConfiguration + Configure<T>; assert full order.
// Coverage index: pathway.env-var-precedence.priority-chain.full-order
```

- **Risk vs reward:** Medium effort (multi-layer IConfiguration setup).
  Highest guard value of the env-var-precedence scenarios because it pins the
  full chain that declarative config (Issue 15) must slot into correctly.

### R4: env var beats appsettings.json in host IConfiguration

- **Target test:**
  `OtlpExporterOptions_EnvVar_BeatsAppsettingsJson_WhenBothSet`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - register two `AddInMemoryCollection` layers with the
  same `OTEL_EXPORTER_OTLP_ENDPOINT` key, lower-priority layer acting as
  appsettings, higher-priority layer acting as env var. Resolve
  `IOptions<OtlpExporterOptions>` and assert the env-var-layer value wins.
- **Guards issues:** 1, 15
- **Risks pinned:** 1.2
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - layered IConfiguration; env-var layer beats appsettings layer.
// Coverage index: pathway.env-var-precedence.env-var.beats-appsettings
```

- **Risk vs reward:** Low effort. Documents a standard .NET behavior dependency
  that is not obvious from the SDK code alone.

### R5: Standalone SDK has no appsettings.json source

- **Target test:**
  `BatchExportProcessorOptions_Standalone_OnlyEnvVarsAvailable`
- **Location:** `BatchExportActivityProcessorOptionsTests.cs`
- **Tier:** 1
- **Observation:** `DirectProperty` - construct with an IConfiguration from
  `AddEnvironmentVariables()` only; assert that a key present only in a
  hypothetical appsettings section does not resolve.
- **Guards issues:** 15
- **Risks pinned:** 1.2
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DirectProperty - IConfiguration with AddEnvironmentVariables() only;
// appsettings keys are invisible in this scenario.
// Coverage index: pathway.env-var-precedence.standalone.env-vars-only
```

- **Risk vs reward:** Low effort. Establishes the standalone SDK boundary before
  Issue 15 adds a new IConfigurationProvider.

## Guards issues

- **Issue 1** - `IValidateOptions<T>`: validation runs as step 4 in
  `DelegatingOptionsFactory.Create`, after `Configure<T>`. Precedence tests
  must exist before the validation step can be tested meaningfully.
- **Issue 5** - PostConfigure cascade for `SdkLimitOptions`: adds a new
  `PostConfigure<T>` step (step 3) to the factory pipeline. The ordering tests
  here pin the chain before that step is inserted.
- **Issue 15** - Declarative config YAML `IConfigurationProvider`: must sit
  at the correct priority relative to env vars, appsettings, and `Configure<T>`.
  The tests in Section 3 are the baseline that verifies priority placement.
