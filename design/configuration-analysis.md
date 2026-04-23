# OpenTelemetry .NET SDK - Configuration Analysis

**Date:** 2026-04-13 **Author:** Steve Gordon (with AI-assisted research)
**Driver:**
[open-telemetry/opentelemetry-dotnet#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)
-- "Add Configuration SDK"

**Companion documents:**

- [Deep Dives](configuration-analysis-deep-dives.md) - detailed analysis of
  specific subsystems (OTLP reload, sampler reloadability, SdkLimitOptions,
  component registry, AOT, telemetry policies, spec alignment)
- [Risk Register](configuration-analysis-risks.md) - all identified risks,
  grouped by the phase in which they must be addressed
- [Proposed Issues](configuration-proposed-issues.md) - 26 sub-issues across 6
  work streams with dependency graph and phasing

---

## 1. Executive Summary

This document audits the configuration landscape of the OpenTelemetry .NET SDK
to establish a clear picture of what exists today, identify gaps, and lay out a
path toward declarative configuration, telemetry policies, and dynamic reload.

**Strategic goal:** Declarative config (OTEL spec) -> Telemetry policies (OTEP
4738) with pluggable policy sources (file-based, OpAMP-backed, custom)

### Key Findings

1. **Consistent IConfiguration foundation.** No SDK component reads env vars
   directly - all env var values are consumed via `IConfiguration` extension
   helpers. The env var `IConfigurationProvider` itself is a vendored copy of
   the runtime's `EnvironmentVariablesConfigurationProvider` (see
   [S2.1](#21-configuration-infrastructure)). This is excellent for composing
   hierarchical config sources.

2. **DelegatingOptionsFactory is the central pattern.** 11 options classes use
   it. The priority model (factory -> Configure -> PostConfigure -> Validate) is
   already correct for layered config.

3. **Zero active OnChange listeners.** No component in the SDK subscribes to
   options changes. `SingletonOptionsManager` explicitly prevents it for logger
   options.

4. **Options consumed once at build time.** All providers, processors, and
   exporters read options at construction and cache the values. Dynamic reload
   requires change propagation plumbing.

5. **10 spec env vars not implemented.** The 5 high-priority ones
   (`OTEL_PROPAGATORS`, `OTEL_*_EXPORTER`, `OTEL_CONFIG_FILE`) are all
   prerequisites for declarative config support.

6. **SdkLimitOptions architectural misplacement.** Lives in the OTLP exporter
   project but contains spec-level limits that should apply to all exporters. ->
   [Deep Dive
   G](configuration-analysis-deep-dives.md#g-sdklimitoptions-architecture-and-path-forward)

7. **Sampler, Resource, Propagator configuration gaps.** No options classes
   exist for these. Samplers have env var support but no `IOptions<T>`
   integration. Resources and propagators are purely programmatic.

8. **`DelegatingOptionsFactory` can be simplified.** The M.E.Options 5.0.0
   threshold for using the virtual `CreateInstance` method has been met across
   all TFMs (minimum resolved is 8.0.0). -> [Deep Dive
   B](configuration-analysis-deep-dives.md#b-delegatingoptionsfactory-simplification)

9. **`OpenTelemetryLoggerOptions` can be refactored.** A non-breaking approach
    extracts accumulated state (`ProcessorFactories`, `ResourceBuilder`) at
    setup time, making the type a pure value object compatible with reload.

---

## 2. Current Landscape

### 2.1 Configuration Infrastructure

**`DelegatingOptionsFactory<T>`** - Custom `OptionsFactory<T>` replacement
ensuring options constructors receive `IConfiguration` **before** any
`Configure<T>()` delegates run. 11 options classes use this. Priority model:
Factory delegate -> `Configure<T>` -> `PostConfigure<T>` -> `Validate<T>`. -> [Deep
Dive
B](configuration-analysis-deep-dives.md#b-delegatingoptionsfactory-simplification)
covers the simplification opportunity.

**`SingletonOptionsManager<T>`** - Disables options reloading for
`OpenTelemetryLoggerOptions`. Blocks reload today but can be eliminated via
non-breaking refactoring.

**Named Options** - Used pervasively. All exporter builder extension methods use
`name ??= Options.DefaultName`. OTLP has a unique per-signal pattern where
`OtlpExporterBuilderOptions` creates four named instances.

**Config Extension Helpers** - Internal `TryGet*Value` methods read all values
from `IConfiguration`, meaning env var keys automatically work from any
configuration source (appsettings.json, user secrets, etc.).

**Vendored `EnvironmentVariablesConfigurationProvider`** - Manually vendored
copy in `src/Shared/EnvironmentVariables/` with no automated sync mechanism;
divergence risk. -> [Risk
1.7](configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy).
See [#7141](https://github.com/open-telemetry/opentelemetry-dotnet/issues/7141)

### 2.2 Options Classes Inventory

15 options classes across 5 packages. Key patterns:

- **11 use `DelegatingOptionsFactory`** - these already support the full
  priority model (factory -> Configure -> PostConfigure -> Validate)
- **3 exporters lack `DelegatingOptionsFactory` integration** - Console,
  Prometheus AspNetCore, Prometheus HttpListener
- **Zero have any reload support** - all read options once at construction
- **`SdkLimitOptions` is architecturally misplaced** - lives in OTLP exporter
  but contains spec-level limits
- **`ExperimentalOptions` is immutable** - all get-only properties; needs
  setters before it can participate in reload

-> [Deep Dive A](configuration-analysis-deep-dives.md#a-options-classes-detail)
has the full inventory table and per-class breakdowns (constructor parameters,
env var mappings, DI registration chains, reload candidacy).

### 2.3 Provider-Level and Cross-Cutting Configuration

- **Resource** - No `ResourceOptions` class. Env var detectors read once at
  build time. `OTEL_RESOURCE_DETECTORS` not implemented.
- **Sampler** - Env vars supported but no `SamplerOptions` class - no
  `IOptions<T>` integration, no reload.
- **Propagator** - Hardcoded default (W3C TraceContext + Baggage).
  `OTEL_PROPAGATORS` not implemented. No `PropagatorOptions` class.
- **Provider-level flags** - `OTEL_SDK_DISABLED` and
  `OTEL_METRICS_EXEMPLAR_FILTER` read once at startup. `OTEL_TRACES_EXPORTER` /
  `OTEL_METRICS_EXPORTER` / `OTEL_LOGS_EXPORTER` - **not implemented**.

### 2.4 Spec Env Var Completeness

Coverage of implemented spec env vars is good - all major `OTEL_BSP_*`,
`OTEL_BLRP_*`, `OTEL_ATTRIBUTE_*_LIMIT`, `OTEL_METRIC_EXPORT_*`, and
`OTEL_EXPORTER_OTLP_*` families are supported. -> [Deep Dive
A.0](configuration-analysis-deep-dives.md#a0-summary-tables) has the full
implemented env vars table.

**High-priority gaps (prerequisites for declarative config):**

| Env Var                 | Why It Matters                                       |
| ----------------------- | ---------------------------------------------------- |
| `OTEL_PROPAGATORS`      | Required for declarative config propagator selection |
| `OTEL_TRACES_EXPORTER`  | Required for declarative config exporter selection   |
| `OTEL_METRICS_EXPORTER` | Required for declarative config exporter selection   |
| `OTEL_LOGS_EXPORTER`    | Required for declarative config exporter selection   |
| `OTEL_CONFIG_FILE`      | Core of declarative config feature                   |

**Other unimplemented:** `OTEL_LOG_LEVEL` (Medium - .NET uses EventSource),
`OTEL_EXPORTER_ZIPKIN_TIMEOUT` (Low), `OTEL_EXPORTER_PROMETHEUS_HOST`/`_PORT`
(Low), `OTEL_ENTITIES` (Low - new spec).

**Java SDK comparison:** Java has full declarative YAML config (experimental),
SPI-based `ComponentProvider`, and env var-based exporter/propagator selection.
The key architectural difference is SPI (classpath scanning) vs .NET DI
(explicit registration) - both serve the same purpose but .NET's approach is
AOT-safer.

---

## 3. Reload Readiness Assessment

### 3.1 Zero OnChange Listeners Today

No provider, processor, or exporter in the SDK subscribes to options changes.
All options are read once at construction and values are cached (or baked into
`readonly` fields). `SingletonOptionsManager` explicitly blocks reload for
`OpenTelemetryLoggerOptions`. Enabling runtime reload requires adding
change-propagation plumbing throughout the stack - it is not a flag to flip.

### 3.2 Per-Class Reload Candidacy

Every options class was assessed for reload feasibility. No class has any reload
support today. Properties divide cleanly across the three tiers: batch intervals
and SDK limits are Tier 1 (value swap), OTLP endpoint/headers are Tier 2
(component refresh), and protocol/processor-type changes are Tier 3 (requires
restart). The lowest-effort wins are `SdkLimitOptions` (all mutable) and the
batch processor intervals. The highest-effort item is `ExperimentalOptions` (all
get-only properties - needs redesign before participating in reload).

-> [Deep Dive A.0](configuration-analysis-deep-dives.md#a0-summary-tables) has
the full per-class reload candidacy table.

### 3.3 Reload Tiers

Not all options need reload support. Priority ordering:

- **Tier 1 - Value reload (immediate):** Sampling rate, export intervals, SDK
  limits, timeouts, headers. The property value changes; the running component
  reads the new value on its next cycle.
- **Tier 2 - Component refresh:** Endpoints (recreate HTTP client), TLS
  certificates. Requires creating a new internal client while draining the old
  one.
- **Tier 3 - Requires restart:** Protocol changes, processor type changes,
  exporter selection, propagator selection. These change the component graph and
  cannot be hot-swapped.

### 3.4 AOT Compatibility Summary

Two existing violations identified:

1. **`OtlpExporterBuilder.cs:153`** - Four calls to
   `services.Configure<T>(IConfiguration)` invoke `ConfigurationBinder`
   reflection internally with no `[UnconditionalSuppressMessage]`. This is an
   unmitigated IL2026/IL3050 bug in AOT-published apps today.

2. **`OpenTelemetryLoggingExtensions.cs`** - Uses
   `services.AddOptions<OpenTelemetryLoggerOptions>().Bind(section)` with an
   `[UnconditionalSuppressMessage]` suppression. Currently safe but fragile --
   any new complex property on `OpenTelemetryLoggerOptions` would break under
   AOT.

-> [Deep Dive
F](configuration-analysis-deep-dives.md#f-aot-compatibility-full-analysis) has
the full analysis and fix options.

---

## 4. Design Direction

### 4.1 Recommendations for Step 2 (Config Provider Registration)

1. **Create options classes for gaps:** `SamplerOptions`, `ResourceOptions`,
   `PropagatorOptions` - following the existing `DelegatingOptionsFactory`
   pattern with env var support in constructors.

2. **Move `SdkLimitOptions` to core SDK.** Non-breaking paths exist. -> [Deep
   Dive
   G](configuration-analysis-deep-dives.md#g-sdklimitoptions-architecture-and-path-forward)

3. **Design an `IConfigurationProvider` for declarative config.** Per
   @martincostello's suggestion, the YAML/JSON config file should participate as
   an `IConfigurationSource` in the standard .NET pipeline rather than replacing
   it.

4. **Define reload granularity.** Use the [Tier 1/2/3 model](#33-reload-tiers)
   above.

5. **Establish `OnChange` listener pattern.** Design a standard pattern for how
   components subscribe to options changes, applied incrementally per [S4.5
   build order](#45-recommended-build-order).

6. **Address `ExperimentalOptions` immutability.** Needs setters or redesigned
   construction before participating in reload.

7. **Implement `OTEL_*_EXPORTER` and `OTEL_PROPAGATORS`.** Use a registry
   pattern (name -> factory) similar to Java's `ComponentProvider` but using .NET
   DI conventions.

8. **Replace the vendored `EnvironmentVariablesConfigurationProvider` with a
   direct package dependency.** The manually vendored copy in
   `src/Shared/EnvironmentVariables/` has no automated sync mechanism and risks
   silent divergence from the runtime.
   `Microsoft.Extensions.Configuration.EnvironmentVariables` is lightweight and
   introduces no new transitive dependencies. -> [Risk
   1.7](configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)

9. **Separate telemetry policy abstractions from implementations.** Policy
   abstractions and `OnChange` infrastructure belong in the core SDK. Concrete
   policy sources (OpAMP-backed, file-based) should ship as separate opt-in
   NuGet packages to avoid forcing transport dependencies on all consumers. See
   [S4.4](#44-telemetry-policies-architecture) and [Deep Dive
   H](configuration-analysis-deep-dives.md#h-telemetry-policies-architecture).

10. **Fix the `OtlpExporterBuilder.cs` AOT bug.** Replace
    `services.Configure<T>(IConfiguration)` with the options constructor
    key-read pattern.

11. **Require the constructor key-read pattern for all factory `Create`
    implementations.** The AOT-safe canonical pattern for vendor extensibility.

### 4.2 Declarative Config Path

The declarative config spec expects YAML like:

```yaml
file_format: "0.4"
sdk:
  traces:
    sampler:
      type: parentbased_traceidratio
      arg: 0.5
    exporters:
      - otlp:
          endpoint: https://collector.example.com:4317
          protocol: grpc
```

To support this in .NET idiomatically:

1. Parse YAML -> build `IConfiguration` tree (via custom
   `IConfigurationProvider`)
2. Map spec keys to existing options properties (e.g.,
   `sdk:traces:processors:batch:schedule_delay` ->
   `BatchExportActivityProcessorOptions.ScheduledDelayMilliseconds`)
3. Use the existing `DelegatingOptionsFactory` + `Configure<T>` priority model
4. Registry pattern for `type: otlp` -> resolve component factory

This reuses all existing infrastructure while adding the declarative config
source as just another `IConfigurationProvider` in the pipeline.

### 4.3 Component Registry Overview

A named factory registry (one interface per component category, e.g.,
`ISpanExporterFactory`) resolves declarative config type names (e.g., `"otlp"`)
to implementations via DI. This replaces Java's classpath-scanning
`ComponentProvider` with explicit DI registration - simpler, AOT-safe, and
idiomatic .NET. Each factory receives the `IConfiguration` subtree rooted at its
YAML node and binds onto existing options classes.

-> [Deep Dive
E](configuration-analysis-deep-dives.md#e-component-registry-detailed-design)
has the full design: all interfaces, DI wiring, named options integration,
vendor extensibility, and AOT implications.

### 4.4 Telemetry Policies Architecture

Telemetry policies (OTEP #4738) define *what* can change at runtime (sampling
rate, export enable/disable, SDK limits). The SDK should own the **abstractions
and `OnChange` plumbing**; concrete policy sources (file-based, OpAMP-backed,
custom) should ship as **separate opt-in NuGet packages** to avoid forcing
transport dependencies on all consumers. The existing `IOptionsMonitor<T>`
infrastructure eliminates the need for a separate PolicyStore - it already is
one.

-> [Deep Dive
H](configuration-analysis-deep-dives.md#h-telemetry-policies-architecture) has
the full design: package breakdown, Java concept mapping, end-to-end flow
diagram, and OpAMP considerations.

### 4.5 Recommended Build Order

Each step is independently shippable and does not require revisiting earlier
steps.

| Step | What                                                                                             | Prerequisite | Unblocks                             |
| ---- | ------------------------------------------------------------------------------------------------ | ------------ | ------------------------------------ |
| 0a   | `IValidateOptions<T>` + `ValidateOnStart` for existing options                                   | None         | Safe declarative config; safe reload |
| 0b   | Standard `OnChange` subscriber pattern (shared infrastructure)                                   | None         | All reload subscribers               |
| 0c   | `TestConfigurationProvider` for reload testing                                                   | None         | All reload test scenarios            |
| 1    | `SamplerOptions` with env-var constructor + validation                                           | 0a           | Declarative config sampler support   |
| 2a   | Telemetry policy abstractions + base `IConfigurationProvider`/`IConfigurationSource` in core SDK | 0c           | All reload scenarios                 |
| 2b   | File-based policy source package *(optional - low risk to bundle)*                               | 2a           | File-watch reload testing            |
| 2c   | OpAMP-backed policy source package *(separate NuGet - opt-in)*                                   | 2a           | OpAMP-driven reload                  |
| 3    | `ReloadableSampler` + wire `IOptionsMonitor<SamplerOptions>` in `TracerProviderSdk`              | 0b, 1, 2     | Sampling rate reload                 |
| 4    | Export enable/disable (`volatile bool` + `OnChange`) in `BatchExportProcessor`                   | 0b, 2        | Kill-switch                          |
| 5    | `OnChange` wiring for `SdkLimitOptions` in OTLP serializers                                      | 0b, 2        | SDK limits reload                    |
| 6    | `OnChange` wiring for batch and metric export intervals                                          | 0b, 2        | Interval reload                      |
| 7    | `ReloadableOtlpExportClient` + internal `IOptionsMonitor`-aware OTLP constructor                 | 0b, 2        | OTLP endpoint/token failover         |

Steps 0a, 0b, 0c, 1, and 2 are independent and can be worked in parallel.

---

## 5. Key Risks and Open Questions

### 5.1 Spec Alignment - Configuration SDK Operations

The [Configuration SDK
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk.md)
defines three explicit operations and several structural concepts that the
current design must address. The spec allows language-idiomatic implementations,
but the design should take a deliberate position on each.

#### 5.1.1 In-Memory Configuration Model

The spec says SDKs SHOULD provide an in-memory representation of the
configuration data model that reflects its schema. The current design proposes
YAML to `IConfiguration` (a schemaless key-value bag) as the sole intermediate
representation. The spec says implementations should be "idiomatic for their
language," so `IConfiguration` could qualify, but it does not provide the typed,
schema-aware model the spec envisions.

**Open question:** Should .NET implement a typed in-memory model (e.g. an
`OpenTelemetryConfiguration` class graph mirroring the spec schema) in addition
to the `IConfiguration` pipeline, or is `IConfiguration` alone sufficient?

**Option A - `IConfiguration` only (current proposal):**

- Reuses all existing infrastructure (`DelegatingOptionsFactory`, named options,
  `Configure`/`PostConfigure`)
- Naturally composable with `appsettings.json`, user secrets, and other .NET
  configuration sources
- Users can mix YAML config with programmatic overrides seamlessly via the
  standard .NET priority model
- Less code to write and maintain
- Schema validation must be done separately (no typed model to validate
  against) - see [Deep Dive
  I.2](configuration-analysis-deep-dives.md#i2-schema-validation-requirements)
  for the two-layer validation requirement
- No standalone "parse and inspect" workflow - the model only exists within the
  DI pipeline
- Schemaless: typos in YAML keys silently produce default values unless
  additional validation is added

**Option B - Typed model as the sole representation (closest to Java):**

- Enables Parse as a standalone validation and inspection operation
- Schema validation falls out naturally from deserialization
- Supports "parse, inspect, modify, then create" workflows
- Requires building and maintaining a parallel type hierarchy mirroring the spec
  schema
- Does not compose with `appsettings.json`, user secrets, or other
  `IConfigurationSource` providers without a separate mapping layer
- Loses the existing `DelegatingOptionsFactory` priority model - the typed
  model would need its own override/merge semantics
- Not idiomatic .NET - no other .NET library uses this approach for
  configuration

**Option C - Typed model for Parse, `IConfiguration` for Create (pragmatic
middle ground):**

- Parse deserializes YAML into a typed `OpenTelemetryConfiguration` model for
  validation and inspection
- Create projects that model into `IConfiguration` keys so the existing options
  pipeline handles the rest
- Gives standalone Parse for config validation tooling (e.g. CI checks, editor
  plugins)
- Preserves the full `DelegatingOptionsFactory` priority model and composability
  with other `IConfigurationSource` providers
- Two representations to maintain (typed model and `IConfiguration` projection),
  though the typed model can be generated from the spec's JSON Schema
- The projection layer is an additional mapping that must stay in sync with both
  the spec schema and the options class properties

**Recommendation:** Option C provides the best balance. The typed model serves
as the validation and tooling layer; `IConfiguration` remains the integration
layer that the rest of the SDK already understands. The typed model can be
generated from the spec's JSON Schema, reducing maintenance burden.

#### 5.1.2 Parse and Create as Explicit Operations

The spec defines Parse (file to in-memory model) and Create (in-memory model
to SDK components) as distinct, user-facing, stateless pure functions. The
current design embeds both into the DI pipeline: `UseDeclarativeConfiguration()`
implicitly parses YAML (adds it as `IConfigurationSource`) and creates
components (during DI container build).

**Open question:** Should Parse and Create be exposed as explicit public APIs,
or is the DI-embedded approach sufficient?

Scenarios that benefit from explicit operations:

- Validating a config file without creating the SDK (CI/CD checks, editor
  tooling)
- Programmatically inspecting or modifying the parsed model before creating
  components (the spec shows a merge example with two config files)
- Unit testing config file parsing independently of SDK construction

Scenarios where the DI-embedded approach is sufficient:

- Standard application startup via `AddOpenTelemetry()` builder
- Mixing declarative config with programmatic overrides (the common case)

The DI-embedded approach already handles the common case. If Option C from
[5.1.1](#511-in-memory-configuration-model) is adopted, Parse naturally becomes
a standalone callable function that returns the typed model. Create can remain
embedded in the DI pipeline, with an optional public overload that accepts the
typed model for advanced scenarios.

#### 5.1.3 ConfigProvider and ConfigProperties (Development Status)

The spec defines a `ConfigProvider` API (currently Development status) that
allows instrumentation libraries to read configuration from the
`.instrumentation` node in the YAML schema at runtime. This concept is absent
from the current design.

In .NET, the natural equivalents are:

| Spec concept       | .NET equivalent                                                            |
| ------------------ | -------------------------------------------------------------------------- |
| `ConfigProvider`   | `IConfiguration` scoped to the `.instrumentation` section                  |
| `ConfigProperties` | `IConfigurationSection` (provides typed accessors, nested sections, etc.)  |

**Proposed approach:** When the YAML `IConfigurationProvider` parses the config
file, the `.instrumentation` subtree is available as
`IConfiguration.GetSection("instrumentation")`. The SDK can expose a
convenience method or DI registration that provides instrumentation libraries
with an `IConfigurationSection` scoped to their namespace:

```csharp
// Instrumentation library reads its config during initialization
public class MyHttpClientInstrumentation
{
    public MyHttpClientInstrumentation(IConfiguration instrumentationConfig)
    {
        var section = instrumentationConfig.GetSection("my_http_client");
        _captureHeaders = section.GetValue<bool>("capture_headers");
    }
}
```

This reuses the existing `IConfiguration` infrastructure and provides the
schemaless, language-idiomatic access pattern the spec describes for
`ConfigProperties`. Since `ConfigProvider` is Development status, this can be
deferred until the spec stabilizes, but the design should not preclude it.

#### 5.1.4 YAML Environment Variable Substitution

The spec defines detailed rules for `${VAR:-default}` substitution within YAML
files, including escape sequences (`$$`), type preservation after substitution,
and prohibition of YAML structure injection. This substitution happens during
YAML parsing, before values enter the `IConfiguration` tree.

This is distinct from .NET's `IConfiguration` environment variable provider.
The `IConfiguration` env var provider makes environment variables available as
configuration keys. The YAML substitution replaces `${VAR}` placeholders
within YAML scalar values with the corresponding environment variable value
at parse time.

**Interplay with `IConfiguration` env var loading:** When the user opts into
declarative config via `OTEL_CONFIG_FILE`, the spec states that the config file
takes precedence and environment variable overrides are disabled for properties
defined in the file (see [Risk
3.1](configuration-analysis-risks.md#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option)).
The YAML `${VAR}` substitution is the spec's mechanism for env-var-driven
values within declarative config - it replaces the `IConfiguration`
`EnvironmentVariablesConfigurationProvider` for properties that the YAML file
defines. The custom `IConfigurationProvider` for YAML should:

1. Perform `${VAR:-default}` substitution during parsing per the spec rules
2. When `OTEL_CONFIG_FILE` is active, the SDK should not layer the
   `EnvironmentVariablesConfigurationProvider` for OTel-specific keys above the
   YAML provider - the YAML file's `${VAR}` substitutions are the only path
   for env vars to influence those values

This is non-trivial implementation work. See [Deep Dive
I.1](configuration-analysis-deep-dives.md#i1-yaml-environment-variable-substitution)
for the full implementation requirements.

#### 5.1.5 Schema Validation and `file_format` Versioning

The spec requires two capabilities not yet captured in the design:

1. **Schema validation during Parse.** The spec says Parse SHOULD return an
   error if the parsed file does not conform to the configuration data model
   schema. This is a different layer from `IValidateOptions<T>` validation
   (which validates semantic correctness of individual options values). Schema
   validation catches structural problems: unknown keys, wrong types, missing
   required fields, invalid nesting. See [Deep Dive
   I.2](configuration-analysis-deep-dives.md#i2-schema-validation-requirements)
   for the two-layer validation model.

2. **`file_format` versioning.** The spec requires a `file_format` field at the
   root of every configuration file (e.g. `file_format: "0.4"`). This enables
   schema evolution and backward compatibility. The YAML `IConfigurationProvider`
   must read this field, validate it against supported versions, and fail fast
   with a clear error if the version is unsupported or missing. Version support
   policy (which `file_format` versions to accept) should be documented per SDK
   release.

### 5.2 Risk Summary

The [Risk Register](configuration-analysis-risks.md) catalogues all identified
risks. Below are the highest-impact items, grouped by the phase in which they
must be addressed.

### Must address before Step 2

| Risk                                                     | Impact                                                                  | Detail                                                                                                                       |
| -------------------------------------------------------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| **Options validation is completely absent**              | Invalid YAML values silently produce broken behaviour                   | [Risk 1.1](configuration-analysis-risks.md#11-options-validation-is-completely-absent)                                       |
| **Parameterless constructors bypass declarative config** | Public constructors create isolated `IConfiguration` from env vars only | [Risk 1.4](configuration-analysis-risks.md#14-public-parameterless-constructors-bypass-declarative-config)                   |
| **YAML key translation**                                 | Spec uses `snake_case`; `IConfiguration` keys need mapping              | [Risk 1.6](configuration-analysis-risks.md#16-configuration-key-translation---yaml-snake_case-to-iconfiguration-keys)        |
| **AOT: reflection-based binding**                        | Existing IL2026/IL3050 bug in `OtlpExporterBuilder.cs`                  | [Deep Dive F.3](configuration-analysis-deep-dives.md#f3-current-bug-reflection-based-binding-in-otlpexporterbuildercs)       |
| **Vendored `EnvironmentVariablesConfigurationProvider`** | Manually vendored copy with no sync mechanism; silent divergence risk   | [Risk 1.7](configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)              |

### Must address before reload implementation

| Risk                                    | Impact                                                                 | Detail                                                                                                               |
| --------------------------------------- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| **Non-atomic multi-options reload**     | Torn reads across options types during concurrent changes              | [Risk 2.1](configuration-analysis-risks.md#21-non-atomic-multi-options-reload-torn-reads-across-options-types)       |
| **OnChange lifecycle and disposal**     | Post-dispose callbacks when `IOptionsMonitor` outlives component       | [Risk 2.2](configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal)                          |
| **Hot-path performance**                | `readonly` fields vs live `IOptionsMonitor` reads on every span/metric | [Risk 2.4](configuration-analysis-risks.md#24-hot-path-performance---readonly-fields-vs-live-options-reads)          |
| **Disposal race during component swap** | Drain semantics needed for OTLP client replacement                     | [Risk 2.5](configuration-analysis-risks.md#25-disposal-race-during-component-swap---drain-semantics)                 |

### Must address before telemetry policies (including OpAMP-backed sources)

| Risk                                                 | Impact                                                                       | Detail                                                                                                                        |
| ---------------------------------------------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **`OTEL_CONFIG_FILE` vs `IConfiguration` hierarchy** | Spec says config file disables env vars; .NET model is additive              | [Risk 3.1](configuration-analysis-risks.md#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option)       |
| **Change debouncing**                                | Rapid policy updates can flood `OnChange` callbacks                          | [Risk 3.2](configuration-analysis-risks.md#32-configuration-change-debouncing)                                                |
| **Thundering herd**                                  | Single `IConfigurationProvider.OnReload()` fires all `IOptionsMonitor` types | [Risk 3.3](configuration-analysis-risks.md#33-ioptionsmonitor-change-notification-granularity-thundering-herd)                |

---

## 6. Test coverage

The refactors proposed across the six work streams in
[`configuration-proposed-issues.md`](configuration-proposed-issues.md) change
how the SDK resolves configuration. Before any of those land, a test safety
net locks in today's observable configuration behaviour so that when a test
breaks, the break maps to a known planned issue with a recorded rationale -
rather than a silent semantics drift.

The planning artefacts for that safety net live in a sibling tree:

- Entry document:
  [`configuration-test-coverage.md`](configuration-test-coverage.md) -
  conventions, modalities, tiers, process-isolation and env-var-isolation
  options, snapshot-library comparison, naming, code-comment template,
  scenario-id format.
- Per-options-class files: `configuration-test-coverage/options/` - one
  file per in-scope options class, each following the same inventory ->
  gaps -> recommendations structure.
- Cross-cutting pathway files: `configuration-test-coverage/pathways/` -
  one file per pathway that does not belong to a single options class
  (env-var precedence, named-options resolution, reload no-op baseline,
  AOT binding, vendored env-var parity, provider-global switches,
  host-vs-standalone parity, and others).
- Existing-test inventory:
  [`configuration-test-coverage/existing-tests.md`](configuration-test-coverage/existing-tests.md) -
  facts-only survey of every config-adjacent test across the three
  in-scope test projects. Downstream files reference this inventory
  rather than re-deriving it.

This section is a pointer only. For the "Test strategy" risks introduced
by the safety net itself (runtime impact, env-var globalness, reflection
brittleness, snapshot maintenance cost), see the corresponding subsection in
[`configuration-analysis-risks.md`](configuration-analysis-risks.md#5-test-strategy).
