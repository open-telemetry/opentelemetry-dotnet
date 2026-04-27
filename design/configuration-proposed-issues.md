# Configuration SDK - Proposed Sub-Issues

**Date:** 2026-04-15 **Author:** Steve Gordon (with AI-assisted research)
**Driver:**
[open-telemetry/opentelemetry-dotnet#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)
-- "Add Configuration SDK" **Source:** [Configuration
Analysis](configuration-analysis.md) | [Deep
Dives](configuration-analysis-deep-dives.md) | [Risk
Register](configuration-analysis-risks.md)

---

## Summary

This document breaks the configuration SDK work
([#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380))
into independently shippable sub-issues, organised across six work streams. The
analysis identified three categories of work:

1. **Foundation fixes** - validation, AOT bugs, tech debt, and missing options
   classes that are prerequisites for everything else. These carry no behaviour
   change and can start immediately.
2. **Declarative config** - the component registry, YAML
   `IConfigurationProvider`, and build-time tree walker that enable
   `OTEL_CONFIG_FILE` and spec env var gaps (`OTEL_*_EXPORTER`,
   `OTEL_PROPAGATORS`).
3. **Reload and telemetry policies** - the `OnChange` subscriber infrastructure,
   per-component reload wiring, and policy source abstractions/packages that
   enable runtime configuration changes.

26 issues are proposed. The first wave - Issues 1--8, 11, 17, and 18 (Streams 1--2
plus factory interfaces and reload test infrastructure) - can be worked in
parallel. Issue 9 depends on Issue 11 and starts after that.

---

## Suggested Phasing

| Phase | Focus | Issues | Risk Profile |
| ---- | ---- | ----- | --------- |
| **A - Foundation** | No behaviour change, low risk. Options validation, factory simplification, new options classes, shared reload infrastructure, component factory interfaces. Issue 4 (AOT fix) is not prioritised — dead code from public API; superseded by Step 6. | 1, 2, 3, 5, 6, 7, 8, 11, 17, 18 | Low |
| **B - Declarative Config MVP** | Enables `OTEL_CONFIG_FILE`, spec env var gaps, component resolution. | 9, 10, 12, 13, 14, 15, 16 | Medium |
| **C - Reload Support** | Per-component `OnChange` wiring for sampler, export intervals, SDK limits, OTLP endpoint/headers. | 19, 20, 21, 22, 23, 24 | Medium-High |
| **D - Telemetry Policies** | Concrete policy source packages (file-based, OpAMP-backed). | 25, 26 | Medium |

---

## Dependency Graph

```text
Stream 1 (Foundation):  1, 2, 3, 4, 5, 6  - all independent, start immediately
Stream 2 (Options):     7, 8 - independent, start immediately; 9 depends on 11
Stream 3 (SdkLimits):   10                 - depends on 5
Stream 4 (Declarative): 11 -> 12, 13, 14 -> 15 -> 16
Stream 5 (Reload):      17, 18 -> 19, 20, 21, 22, 23
Stream 6 (Policies):    24 -> 25, 26

Issues 1--8, 11, 17, 18 can be worked in parallel as the first wave (Issue 9 requires Issue 11 first).
```

---

## Stream 1: Foundation & Infrastructure

### Issue 1 - Add `IValidateOptions<T>` for reload protection

**Priority:** P1 - prerequisite for safe reload; implement alongside the first
reload-capable `OnChange` subscriber (Step 9 in
[S4.5](configuration-analysis.md#45-recommended-build-order)) **Refs:** [Risk
1.1](configuration-analysis-risks.md#11-options-validation-is-completely-absent),
[Risk
1.2](configuration-analysis-risks.md#12-silent-configuration-failure-model-vs-fail-fast),
[S4.5 Step 9](configuration-analysis.md#45-recommended-build-order)

The codebase has zero `IValidateOptions<T>` implementations. The validation
pipeline in `DelegatingOptionsFactory` exists but is never exercised.

The primary purpose of validators in this SDK is **reload protection**: when
`IOptionsMonitor.OnChange` fires, `DelegatingOptionsFactory.Create` runs the
validation pipeline before the new options instance is returned. Invalid values
cause `OptionsValidationException`, which the `OnChange` callback catches to
retain the previous valid value. This prevents a bad OpAMP push or a malformed
file reload from silently breaking a running system.

**`ValidateOnStart` must not be registered.** Without the `OnChange` catch blocks
in place, validators run lazily at options access time (provider construction).
A validation failure at that point produces an unhandled `OptionsValidationException`
at startup — worse than the current silent fallback. An instrumentation library
must not prevent the application from running due to a bad telemetry config value.
For startup validation of declarative YAML config, the JSON schema embedded in the
OTel configuration specification is the primary guard (see
[Risk 1.2](configuration-analysis-risks.md#12-silent-configuration-failure-model-vs-fail-fast)).

**Sequencing:** This issue is deferred until the declarative config POC and the
first reload `OnChange` handler exist. The validators and the handler should land
together so the catch block that makes `OptionsValidationException` safe is always
present.

**Prototype first:** Start with `PeriodicExportingMetricReaderOptions`. It has two
unambiguous constraints, no cross-field dependencies, and a clear observable
consequence when invalid values are accepted:

- `ExportIntervalMilliseconds > 0` (zero causes a tight export loop; CPU pegged)
- `ExportTimeoutMilliseconds > 0`

Once the prototype is accepted, expand to:

- `BatchExport*ProcessorOptions`: `MaxQueueSize > 0`, `MaxExportBatchSize > 0 &&
  <= MaxQueueSize`, intervals > 0
- `OtlpExporterOptions`: `TimeoutMilliseconds > 0`, `Endpoint` is valid URI
- `SdkLimitOptions`: count limits > 0 when set
- Future `SamplerOptions`: `SamplerArg` in `[0.0, 1.0]` for ratio-based samplers

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [batch-export-activity-processor-options.md](configuration-test-coverage/options/batch-export-activity-processor-options.md), [batch-export-logrecord-processor-options.md](configuration-test-coverage/options/batch-export-logrecord-processor-options.md), [periodic-exporting-metric-reader-options.md](configuration-test-coverage/options/periodic-exporting-metric-reader-options.md), [opentelemetry-logger-options.md](configuration-test-coverage/options/opentelemetry-logger-options.md), [otlp-mtls-options.md](configuration-test-coverage/options/otlp-mtls-options.md), [otlp-tls-options.md](configuration-test-coverage/options/otlp-tls-options.md), [experimental-options.md](configuration-test-coverage/options/experimental-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [batch-export-processor-options.md](configuration-test-coverage/options/batch-export-processor-options.md), [metric-reader-options.md](configuration-test-coverage/options/metric-reader-options.md), [log-record-export-processor-options.md](configuration-test-coverage/options/log-record-export-processor-options.md), [activity-export-processor-options.md](configuration-test-coverage/options/activity-export-processor-options.md), [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md), [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md)

---

<!-- markdownlint-disable-next-line MD013 -->
### Issue 2 - Simplify `DelegatingOptionsFactory<T>` using `OptionsFactory<T>.CreateInstance` override

**Priority:** P1 - tech debt reduction, simplifies future work **Refs:** [Deep
Dive
B](configuration-analysis-deep-dives.md#b-delegatingoptionsfactory-simplification),
[S2.1](configuration-analysis.md#21-configuration-infrastructure)

**Status: Complete** — tracking issue
[#7147](https://github.com/open-telemetry/opentelemetry-dotnet/issues/7147)
closed; merged in
[#7148](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7148)
(2026-04-24).

The M.E.Options 5.0.0 threshold for the virtual `CreateInstance` method has been
met across all TFMs (minimum resolved is 8.0.0). Replace the full fork of
`OptionsFactory<T>` with a slim subclass:

```csharp
internal sealed class DelegatingOptionsFactory<TOptions> : OptionsFactory<TOptions>
    where TOptions : class
{
    private readonly Func<IConfiguration, string, TOptions> optionsFactoryFunc;
    private readonly IConfiguration configuration;

    public DelegatingOptionsFactory(...)
        : base(setups, postConfigures, validations)
    { ... }

    protected override TOptions CreateInstance(string name)
        => optionsFactoryFunc(configuration, name);
}
```

This removes ~100 lines of duplicated framework code.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md)

---

<!-- markdownlint-disable-next-line MD013 -->
### Issue 3 - Replace vendored `EnvironmentVariablesConfigurationProvider` with package dependency

**Priority:** P1 - eliminates silent divergence risk **Refs:** [Risk
1.7](configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy),
[S4.1 rec
8](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration)

**Status: In progress** — tracking issue
[#7141](https://github.com/open-telemetry/opentelemetry-dotnet/issues/7141);
open PR
[#7146](https://github.com/open-telemetry/opentelemetry-dotnet/pull/7146).

The SDK carries a manually vendored copy of the runtime's
`EnvironmentVariablesConfigurationProvider` in
`src/Shared/EnvironmentVariables/` (three files). There is no automated sync
mechanism - the copy has not been updated since it was introduced in PR #4092
(January 2023).

Remove the three vendored files and add a direct dependency on
`Microsoft.Extensions.Configuration.EnvironmentVariables`. Update `<Compile
Include>` link items in consuming `.csproj` files. Version follows the existing
per-TFM strategy in `Directory.Packages.props` (8.0.0 for net8.0, 9.0.0 for
net9.0, etc.). Verify the public `AddEnvironmentVariables()` extension method
resolves correctly at all call sites (the vendored copy uses `internal`
visibility).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [vendored-env-var-parity.md](configuration-test-coverage/pathways/vendored-env-var-parity.md)

---

### Issue 4 - Fix AOT bug: reflection-based binding in `OtlpExporterBuilder.cs`

**Priority:** Not prioritised — see analysis note below **Refs:** [Deep Dive
F.3](configuration-analysis-deep-dives.md#f3-current-bug-reflection-based-binding-in-otlpexporterbuildercs),
[S3.4](configuration-analysis.md#34-aot-compatibility-summary), [S4.1 rec
10](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration)

**Analysis:** `OtlpExporterBuilder` is `internal sealed`. The four reflection
calls in `BindConfigurationToOptions` are only reached when
`configuration != null`, which only occurs via `internal` `UseOtlpExporter`
overloads. All public overloads pass `configuration: null`. No consumer
code — whether AOT-published or not — can reach this code path. This is
effectively dead code from a public API perspective and is not an actionable
AOT violation for consumers.

The `BindConfigurationToOptions` approach will be entirely superseded once
declarative config factory support (Step 6) lands. At that point the binding
responsibility moves to the options constructors in a fully AOT-safe way.
Until then, no fix is needed.

Four calls to `services.Configure<T>(IConfiguration)` at
`OtlpExporterBuilder.cs:153` invoke `ConfigurationBinder.Bind()` reflection
internally with no `[UnconditionalSuppressMessage]`. This is an IL2026/IL3050
warning in the library build but not an unmitigated consumer violation.

**Fix (if desired):** Move bindings into the options constructors using the
constructor key-read pattern (`configuration[key]` via the existing
`OpenTelemetryConfigurationExtensions` helpers). The `DelegatingOptionsFactory`
already supplies `IConfiguration` to constructors - no factory wiring change
needed.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [aot-binding.md](configuration-test-coverage/pathways/aot-binding.md)

---

### Issue 5 - Move `SdkLimitOptions` fallback cascade logic to `PostConfigure<T>`

**Priority:** P1 - prerequisite for correct reload and declarative config
behaviour **Refs:** [Risk
1.5](configuration-analysis-risks.md#15-postconfigure-gap-for-fallback-chains-under-reload),
[Deep Dive G.5 Step 2a](configuration-analysis-deep-dives.md#g5-sequencing)

The cascading fallback chain (`SpanEventAttributeCountLimit` ->
`SpanAttributeCountLimit` -> `AttributeCountLimit`) currently runs in the
constructor, before `Configure<T>` delegates execute. Under reload or
declarative config, a user's `Configure<SdkLimitOptions>` delegate might set
`SpanAttributeCountLimit = 50` but the cascade was already evaluated with old
values.

Move the cascade to a `PostConfigure<SdkLimitOptions>` registration:

```csharp
services.PostConfigure<SdkLimitOptions>((options) =>
{
    options.SpanAttributeCountLimit ??= options.AttributeCountLimit ?? 128;
    options.SpanEventAttributeCountLimit ??= options.SpanAttributeCountLimit;
    options.SpanLinkAttributeCountLimit ??= options.SpanAttributeCountLimit;
    // ... remaining cascades
});
```

Also apply the same pattern to `OtlpExporterOptions.AppendSignalPathToEndpoint`
(which depends on whether `Endpoint` was explicitly set).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md), [env-var-fallback-chains.md](configuration-test-coverage/pathways/env-var-fallback-chains.md)

---

### Issue 6 - Add diagnostic logging for `RegisterOptionsFactory` silent skip

**Priority:** P2 - improves debuggability **Refs:** [Risk
1.3](configuration-analysis-risks.md#13-tryaddsingleton-first-wins---silent-misconfiguration-risk)

When `RegisterOptionsFactory<T>` detects a pre-existing `IOptionsFactory<T>`
registration and skips its own via `TryAddSingleton`, the skip is completely
invisible. Log a diagnostic event via `OpenTelemetrySdkEventSource` identifying
the existing registration type so the user can determine whether the conflict is
intentional.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [otlp-mtls-options.md](configuration-test-coverage/options/otlp-mtls-options.md), [try-add-singleton-first-wins.md](configuration-test-coverage/pathways/try-add-singleton-first-wins.md), [observability-and-silent-failures.md](configuration-test-coverage/pathways/observability-and-silent-failures.md)

---

## Stream 2: New Options Classes

<!-- markdownlint-disable-next-line MD013 -->
### Issue 7 - Add `SamplerOptions` with env-var constructor and `DelegatingOptionsFactory` registration

**Priority:** P0 - prerequisite for declarative config sampler support and
sampler reload **Refs:** [Deep Dive
D.4--D.5](configuration-analysis-deep-dives.md#d4-justification-across-scenarios),
[S4.1 rec
1](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration),
[S4.5 Step 4](configuration-analysis.md#45-recommended-build-order) **Depends
on:** None (but should coordinate with Issue 1 for validator)

Create `SamplerOptions` with properties:

- `SamplerType` (string?) - maps to `OTEL_TRACES_SAMPLER`
- `SamplerArg` (double?) - maps to `OTEL_TRACES_SAMPLER_ARG`

Read env vars via `IConfiguration` in the constructor following the existing
pattern. Register via `DelegatingOptionsFactory`. Include
`IValidateOptions<SamplerOptions>` (`SamplerArg` in `[0.0, 1.0]` for
ratio-based). Refactor `TracerProviderSdk.GetSampler()` to consume
`SamplerOptions` instead of reading `IConfiguration` directly.

This is a non-breaking addition with no runtime behaviour change. It delivers
`appsettings.json` section binding and the `DelegatingOptionsFactory` priority
model for free. It is a prerequisite for both declarative config sampler support
(the factory needs an options class to bind into) and runtime reload.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** `SamplerOptions` is new surface; tests are authored with the issue. Integration baselines for the pathways the new class joins: [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md), [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md).

---

### Issue 8 - Add `ResourceOptions` with env-var constructor

**Priority:** P1 - prerequisite for declarative config resource support
**Refs:** [Risk
2.9](configuration-analysis-risks.md#29-resource-detector-declarative-config-gap),
[S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration),
[S4.1 rec
1](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration)
**Depends on:** None

Create `ResourceOptions` with properties:

- `ServiceName` (string?) - maps to `OTEL_SERVICE_NAME`
- `ResourceAttributes` (string?) - maps to `OTEL_RESOURCE_ATTRIBUTES`
  (comma-separated)
- `EnabledDetectors` (string[]?) - maps to `OTEL_RESOURCE_DETECTORS` (future)

Use `IOptions<ResourceOptions>` (not `IOptionsMonitor` - resources don't change
at runtime). Refactor `ResourceBuilder` to consume `ResourceOptions` when
available via DI.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** `ResourceOptions` is new surface; tests are authored with the issue. Integration baselines for the pathways the new class joins: [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md), [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md).

---

### Issue 9 - Add `PropagatorOptions` and implement `OTEL_PROPAGATORS`

**Priority:** P1 - prerequisite for declarative config propagator selection
**Refs:**
[S2.3](configuration-analysis.md#23-provider-level-and-cross-cutting-configuration),
[S2.4 high-priority
gaps](configuration-analysis.md#24-spec-env-var-completeness), [S4.1 recs 1 &
7](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration)
**Depends on:** Issue 11 (for `ITextMapPropagatorFactory` interface)

Create `PropagatorOptions` with a `Propagators` property (string list).
Implement `OTEL_PROPAGATORS` env var parsing. Use the factory registry (Issue
11's `ITextMapPropagatorFactory`) for propagator name resolution. Register
built-in propagator factories: `tracecontext`, `baggage`, `b3`, `b3multi`,
`jaeger`.

This closes one of the five high-priority spec env var gaps.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** `PropagatorOptions` and `OTEL_PROPAGATORS` handling are new surface; tests are authored with the issue. Integration baselines for the pathways the new class joins: [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md), [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md), [provider-global-switches.md](configuration-test-coverage/pathways/provider-global-switches.md).

---

## Stream 3: SdkLimitOptions Architecture

### Issue 10 - Add public `SdkLimitsOptions` to core SDK package

**Priority:** P1 - unblocks declarative config limit support for all exporters
**Refs:** [Deep Dive G.4 Options B &
D](configuration-analysis-deep-dives.md#g4-non-breaking-design-options), [Deep
Dive G.5 Steps 1--2](configuration-analysis-deep-dives.md#g5-sequencing)
**Depends on:** Issue 5 (PostConfigure cascade pattern)

**Step 1:** Add well-known `IConfiguration` key constants to core SDK:

```csharp
public static class SdkConfigurationKeys
{
    public const string AttributeCountLimit = "OTEL_ATTRIBUTE_COUNT_LIMIT";
    // ... all ten keys
}
```

**Step 2:** Add a new public `SdkLimitsOptions` class following the
`DelegatingOptionsFactory` pattern with all ten properties as simple nullable
auto-props (no fallback chain - that lives in `PostConfigure`). The
OTLP-internal `SdkLimitOptions` checks DI for `SdkLimitsOptions` and defers to
it when present; falls back to its own env var reading when absent (preserving
current behaviour exactly).

Non-breaking: other exporters (`Console`, `Zipkin`) can opt in incrementally to
reading from `SdkLimitsOptions`.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [env-var-fallback-chains.md](configuration-test-coverage/pathways/env-var-fallback-chains.md)

---

## Stream 4: Component Registry & Declarative Config

### Issue 11 - Define component factory interfaces in core SDK

**Priority:** P0 - central abstraction for declarative config **Refs:** [Deep
Dive E.2](configuration-analysis-deep-dives.md#e2-factory-interface-design),
[S4.3](configuration-analysis.md#43-component-registry-overview) **Depends on:**
None

Add to the `OpenTelemetry` package:

```csharp
public interface ISpanExporterFactory
{
    string Name { get; }
    BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services);
}

public interface IMetricExporterFactory { ... }
public interface ILogRecordExporterFactory { ... }
public interface ISamplerFactory { ... }
public interface ITextMapPropagatorFactory { ... }
public interface IResourceDetectorFactory { ... }
```

The `IConfiguration` argument is the subtree rooted at that component's config
node, not the root configuration. Document the AOT-safe constructor key-read
pattern ([Deep Dive
F.5](configuration-analysis-deep-dives.md#f5-required-fix-to-vendor-extensibility-pattern))
as the required implementation approach - `configuration.Bind(options)` must not
appear in any factory `Create` implementation.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** factory interfaces are new surface; tests are authored with the issue. Integration baselines for the pathways the new abstractions join: [aot-binding.md](configuration-test-coverage/pathways/aot-binding.md), [named-options-resolution.md](configuration-test-coverage/pathways/named-options-resolution.md).

---

### Issue 12 - Implement built-in sampler factories

**Priority:** P1 **Refs:** [Deep Dive D.5 Step
2](configuration-analysis-deep-dives.md#d5-sequencing), [Deep Dive
E.5](configuration-analysis-deep-dives.md#e5-component-resolution-at-sdk-build-time)
**Depends on:** Issues 7, 11

Implement `ISamplerFactory` for the six built-in sampler types: `always_on`,
`always_off`, `traceidratio`, `parentbased_always_on`, `parentbased_always_off`,
`parentbased_traceidratio`. Register via `TryAddEnumerable`. Each factory
creates the sampler from its `IConfiguration` subtree using `SamplerOptions`.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** built-in sampler factories are new surface; tests are authored with the issue. Integration baselines for the pathways the factories join: [delegating-options-factory-priority.md](configuration-test-coverage/pathways/delegating-options-factory-priority.md), [named-options-resolution.md](configuration-test-coverage/pathways/named-options-resolution.md), [aot-binding.md](configuration-test-coverage/pathways/aot-binding.md).

---

### Issue 13 - Implement `OTEL_TRACES_EXPORTER`, `OTEL_METRICS_EXPORTER`, `OTEL_LOGS_EXPORTER`

**Priority:** P1 **Refs:** [S2.4 high-priority
gaps](configuration-analysis.md#24-spec-env-var-completeness), [S4.1 rec
7](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration),
[Deep Dive
E.7](configuration-analysis-deep-dives.md#e7-unifying-env-var-and-declarative-file-config)
**Depends on:** Issue 11

Using the component factory interfaces, implement the three `OTEL_*_EXPORTER`
env vars. Read the value via `IConfiguration`, resolve
`IEnumerable<I*ExporterFactory>`, find matching `Name`, call `Create`. This
unifies the env var path and the future declarative file path through the same
factory resolution (Deep Dive E.7).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** the `OTEL_*_EXPORTER` env vars are new surface; tests are authored with the issue. Integration baselines for the pathways the env vars join: [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md), [env-var-fallback-chains.md](configuration-test-coverage/pathways/env-var-fallback-chains.md), [named-options-resolution.md](configuration-test-coverage/pathways/named-options-resolution.md).

---

### Issue 14 - Register OTLP exporter component factories

**Priority:** P1 **Refs:** [Deep Dive
E.4](configuration-analysis-deep-dives.md#e4-registration-model-aot-safe), [Deep
Dive E.8](configuration-analysis-deep-dives.md#e8-third-party-extensibility)
**Depends on:** Issue 11

Add `AddOtlpExporterComponents()` extension method to the OTLP exporter package.
Registers `OtlpSpanExporterFactory`, `OtlpMetricExporterFactory`,
`OtlpLogRecordExporterFactory` via `TryAddEnumerable`. Each factory uses the
existing `OtlpExporterOptions` with named options integration ([Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories)):
resolve pre-bound named options via
`IOptionsMonitor<OtlpExporterOptions>.Get(optionsName)`.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [log-record-export-processor-options.md](configuration-test-coverage/options/log-record-export-processor-options.md), [activity-export-processor-options.md](configuration-test-coverage/options/activity-export-processor-options.md), [named-options-resolution.md](configuration-test-coverage/pathways/named-options-resolution.md)

---

### Issue 15 - YAML/JSON declarative config `IConfigurationProvider`

**Priority:** P1 - the core of `OTEL_CONFIG_FILE` support **Refs:**
[S4.2](configuration-analysis.md#42-declarative-config-path), [Risk
1.6](configuration-analysis-risks.md#16-configuration-key-translation---yaml-snake_case-to-iconfiguration-keys),
[Risk
3.1](configuration-analysis-risks.md#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option),
[Risk
4.1](configuration-analysis-risks.md#41-yaml-array-to-iconfiguration-projection)
**Depends on:** Issues 11, 14 (for factory registration pattern to validate
against)

Implement a custom `IConfigurationProvider` / `IConfigurationSource` that:

1. Parses `OTEL_CONFIG_FILE` YAML into an `IConfiguration` tree
2. Maps spec `snake_case` keys to env var equivalents where 1:1 mapping exists
   (Strategy A), and projects structural arrays as hierarchical sections
   (Strategy B hybrid - Risk 1.6)
3. Supports overlay mode (default) and strict mode via
   `AllowConfigurationOverlay` option (Risk 3.1)
4. Handles array-to-IConfiguration projection for propagator/exporter lists
   (Risk 4.1)
5. Provides helper for reading both comma-separated (env var) and indexed (YAML)
   array formats

Expose via
`UseDeclarativeConfiguration(Action<DeclarativeConfigurationOptions>?)` builder
extension. Register at low priority in the `IConfigurationBuilder` so env vars
and `appsettings.json` override by default (Risk 3.5).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [env-var-precedence.md](configuration-test-coverage/pathways/env-var-precedence.md), [host-vs-standalone-parity.md](configuration-test-coverage/pathways/host-vs-standalone-parity.md), [provider-global-switches.md](configuration-test-coverage/pathways/provider-global-switches.md)

---

### Issue 16 - Declarative config build-time tree walker

**Priority:** P1 **Refs:** [Deep Dive
E.5](configuration-analysis-deep-dives.md#e5-component-resolution-at-sdk-build-time),
[Deep Dive
E.3](configuration-analysis-deep-dives.md#e3-named-options-integration-inside-factories),
[Deep Dive
E.6](configuration-analysis-deep-dives.md#e6-multiple-instances-of-the-same-component-type)
**Depends on:** Issues 11, 15

Implement the build-time logic that walks the parsed YAML `IConfiguration` tree
and resolves components via the factory registry. For each YAML node:

1. Resolve `IEnumerable<I*Factory>` from DI
2. Find factory where `Name` matches the YAML key
3. Call `factory.Create(configSubtree, services)`
4. Register result with the appropriate provider builder

Generate position-based named options keys
(`declarative:sdk:traces:processors:0:batch:exporter:otlp`). Fail fast with an
actionable error for unresolvable names. Support multiple instances of the same
component type (Deep Dive E.6).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [provider-global-switches.md](configuration-test-coverage/pathways/provider-global-switches.md), [host-vs-standalone-parity.md](configuration-test-coverage/pathways/host-vs-standalone-parity.md)

---

## Stream 5: Reload Infrastructure

### Issue 17 - Design and implement standard `OnChange` subscriber pattern

**Priority:** P0 - prerequisite for all reload subscribers **Refs:** [S4.5 Step
7](configuration-analysis.md#45-recommended-build-order), [Risk
2.2](configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal),
[Risk
2.3](configuration-analysis-risks.md#23-onchange-callback-exception-safety),
[Risk
2.4](configuration-analysis-risks.md#24-hot-path-performance---readonly-fields-vs-live-options-reads),
[Risk
2.8](configuration-analysis-risks.md#28-onchange-callback-threading-model),
[Risk
4.7](configuration-analysis-risks.md#47-configuration-system-self-observability)
**Depends on:** None

Define and document the canonical `OnChange` subscriber pattern:

1. **Disposal guard** - `if (this.disposed) return`
2. **Name filter** - `name != this.optionsName`
3. **Value-equality guard** - `HasMeaningfulChange(current, incoming)` to avoid
   thundering herd (Risk 3.3)
4. **Exception safety** - try/catch, log via EventSource, retain previous value
5. **Subscription cleanup** - store `IDisposable` token; dispose it in component
   `Dispose()` before internal resources

Add EventSource events to `OpenTelemetrySdkEventSource`:

- `ConfigurationChangeDetected` (Informational)
- `ConfigurationChangeApplied` (Informational)
- `ConfigurationChangeRejected` (Warning)
- `ConfigurationChangeSkipped` (Verbose)

The pattern should be a well-documented shared helper or base that all
reload-capable components use consistently.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [batch-export-activity-processor-options.md](configuration-test-coverage/options/batch-export-activity-processor-options.md), [batch-export-logrecord-processor-options.md](configuration-test-coverage/options/batch-export-logrecord-processor-options.md), [periodic-exporting-metric-reader-options.md](configuration-test-coverage/options/periodic-exporting-metric-reader-options.md), [opentelemetry-logger-options.md](configuration-test-coverage/options/opentelemetry-logger-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [metric-reader-options.md](configuration-test-coverage/options/metric-reader-options.md), [singleton-options-manager.md](configuration-test-coverage/pathways/singleton-options-manager.md), [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md), [observability-and-silent-failures.md](configuration-test-coverage/pathways/observability-and-silent-failures.md)

---

### Issue 18 - Add `TestConfigurationProvider` for reload testing

**Priority:** P0 - prerequisite for all reload test scenarios **Refs:** [S4.5
Step 8](configuration-analysis.md#45-recommended-build-order), [Risk
4.6](configuration-analysis-risks.md#46-testing-infrastructure-for-reload-scenarios)
**Depends on:** None

Build a controllable `TestConfigurationProvider` (extending
`ConfigurationProvider`) as shared test infrastructure. Must support:

- On-demand `OnReload()` firing with specified new key/value pairs
- Synchronisation primitives to wait for `OnChange` callbacks to complete before
  asserting

Include test pattern examples for:

- Valid reload (value change applied)
- Invalid reload (validation rejects, old state retained)
- Rapid successive reloads (final state is consistent, no exceptions)
- Reload during active export (mock exporter with delay; no crash)
- Reload after provider dispose (no callback side-effects)

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

<!-- markdownlint-disable-next-line MD013 -->
### Issue 19 - `ReloadableSampler` and `IOptionsMonitor<SamplerOptions>` wiring in `TracerProviderSdk`

**Priority:** P1 **Refs:** [Deep Dive
D.3](configuration-analysis-deep-dives.md#d3-implementation-approach---reloadablesampler-wrapper),
[S4.5 Step 9](configuration-analysis.md#45-recommended-build-order) **Depends
on:** Issues 7, 17, 18

Add internal `ReloadableSampler` wrapper with `volatile Sampler _inner` and
`UpdateSampler(Sampler)`. Because it is neither `AlwaysOnSampler` nor
`AlwaysOffSampler`, the type switch in `TracerProviderSdk` always routes through
the general `ComputeActivitySamplingResult` branch.

Wire `IOptionsMonitor<SamplerOptions>.OnChange` in `TracerProviderSdk` using the
standard subscriber pattern (Issue 17). The volatile swap is inherently safe (no
drain needed - `ShouldSample` is a pure function).

**Trade-off:** Loses `AlwaysOn`/`AlwaysOff` fast paths when sampler is
configured via options. Callers who use `SetSampler(new AlwaysOnSampler())`
programmatically still get the fast path.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

### Issue 20 - Export enable/disable kill-switch via `OnChange` in `BatchExportProcessor`

**Priority:** P2 **Refs:** [S4.5 Step
10](configuration-analysis.md#45-recommended-build-order) **Depends on:** Issues
17, 18

Add a `volatile bool exportEnabled` field to `BatchExportProcessor`. Wire
`OnChange` to toggle it. When disabled, `TryExport` short-circuits (items
dropped, not queued). This is the "stop all export" emergency mechanism for
telemetry policies.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [log-record-export-processor-options.md](configuration-test-coverage/options/log-record-export-processor-options.md), [activity-export-processor-options.md](configuration-test-coverage/options/activity-export-processor-options.md), [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

### Issue 21 - Wire `OnChange` for batch and metric export intervals

**Priority:** P2 **Refs:** [S4.5 Step
12](configuration-analysis.md#45-recommended-build-order), [Risk
2.4](configuration-analysis-risks.md#24-hot-path-performance---readonly-fields-vs-live-options-reads)
**Depends on:** Issues 17, 18

**`BatchExportProcessor`:** Remove `readonly` from `ScheduledDelayMilliseconds`
and `ExporterTimeoutMilliseconds` (replace with `volatile`). The worker loop's
`WaitHandle.WaitAny` timeout naturally picks up new values on the next cycle.
Leave `MaxExportBatchSize` as restart-required (avoids per-item volatile read on
ARM).

**`PeriodicExportingMetricReader`:** Call `Timer.Change()` on reload to apply
new intervals immediately.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [batch-export-activity-processor-options.md](configuration-test-coverage/options/batch-export-activity-processor-options.md), [batch-export-logrecord-processor-options.md](configuration-test-coverage/options/batch-export-logrecord-processor-options.md), [periodic-exporting-metric-reader-options.md](configuration-test-coverage/options/periodic-exporting-metric-reader-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [metric-reader-options.md](configuration-test-coverage/options/metric-reader-options.md), [log-record-export-processor-options.md](configuration-test-coverage/options/log-record-export-processor-options.md), [activity-export-processor-options.md](configuration-test-coverage/options/activity-export-processor-options.md), [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

### Issue 22 - Wire `OnChange` for `SdkLimitsOptions` in OTLP serializers

**Priority:** P2 **Refs:** [S4.5 Step
11](configuration-analysis.md#45-recommended-build-order) **Depends on:** Issues
10, 17, 18

Replace `readonly SdkLimitOptions` reference in `ProtobufOtlpTraceSerializer`
and `ProtobufOtlpLogSerializer` with a `volatile` reference. Wire `OnChange` to
swap the reference. Already per-batch (not per-span), so the volatile read cost
is negligible.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md), [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

<!-- markdownlint-disable-next-line MD013 -->
### Issue 23 - OTLP exporter reload: `ReloadableOtlpExportClient` and `IOptionsMonitor`-aware constructor

**Priority:** P2 **Refs:** [Deep Dive
C.3--C.5](configuration-analysis-deep-dives.md#c3-solution-approaches), [S4.5
Step 13](configuration-analysis.md#45-recommended-build-order), [Risk
2.5](configuration-analysis-risks.md#25-disposal-race-during-component-swap---drain-semantics)
**Depends on:** Issues 17, 18

Implement the combined Approach A+B from Deep Dive C.5:

1. Add internal `ReloadableOtlpExportClient` wrapper implementing
   `IExportClient`
2. Add internal `OtlpTraceExporter` constructor accepting
   `IOptionsMonitor<OtlpExporterOptions>` + name
3. Make `OtlpExporterTransmissionHandler.TimeoutMilliseconds` mutable (internal
   property)
4. Implement swap-drain-dispose protocol (Risk 2.5):
   - Atomic swap - new exports use new handler immediately
   - Bounded drain - `Shutdown(5000)` on old handler
   - Dispose - safe after drain completes or times out
5. Reject `Protocol` changes at reload time (Tier 3 - log warning via
   EventSource, ignore)
6. Preserve public `OtlpTraceExporter(OtlpExporterOptions)` constructor
   unchanged
7. Leave unnamed-options path as snapshot (no reload) - document explicitly

No breaking public API changes. The behaviour change (reload support for named
OTLP exporters) is strictly additive. Threading note: for the initial
implementation, blocking `OnChange` is acceptable (OTLP config changes are rare
events). If profiling shows issues, offload to `ThreadPool.QueueUserWorkItem`
with a CAS-based sequence guard (Risk 2.8).

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md), [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md), [named-options-resolution.md](configuration-test-coverage/pathways/named-options-resolution.md), [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md)

---

## Stream 6: Telemetry Policy Abstractions

<!-- markdownlint-disable-next-line MD013 -->
### Issue 24 - Telemetry policy abstractions and base `TelemetryPolicyConfigurationProvider` in core SDK

**Priority:** P1 **Refs:** [Deep Dive
H.1--H.3](configuration-analysis-deep-dives.md#h1-design-principle---abstractions-in-sdk-implementations-as-opt-in-packages),
[S4.5 Step 14](configuration-analysis.md#45-recommended-build-order) **Depends
on:** Issues 17, 18

Add to core SDK:

- `TelemetryPolicyConfigurationProvider` base class (extends
  `ConfigurationProvider`)
- `TelemetryPolicyConfigurationSource` base class
- `UpdatePolicies(IDictionary<string, string?> newValues)` method that sets
  `Data` and calls `OnReload()` once atomically (avoids the debouncing risk in
  Risk 3.2)

Builder extension for registration with correct priority ordering - registered
last so runtime policy changes override all static sources (Risk 3.5). These are
the abstractions that concrete policy sources implement.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** telemetry policy abstractions are new surface; tests are authored with the issue. Integration baselines for the pathways the abstractions join: [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md), [observability-and-silent-failures.md](configuration-test-coverage/pathways/observability-and-silent-failures.md).

---

### Issue 25 - File-based telemetry policy source package

**Priority:** P2 **Refs:** [Deep Dive H.1
table](configuration-analysis-deep-dives.md#h1-design-principle---abstractions-in-sdk-implementations-as-opt-in-packages),
[S4.5 Step 15](configuration-analysis.md#45-recommended-build-order) **Depends
on:** Issue 24

Package: `OpenTelemetry.Extensions.TelemetryPolicies.File` *(name TBD)*

Implement a file-watching `TelemetryPolicyConfigurationProvider` that monitors a
policy file (YAML/JSON) on disk. No new dependencies beyond what the SDK already
takes. Expose via `.WithFilePolicySource(path)` builder extension. Could
potentially be bundled in core SDK given zero new deps, but a separate package
keeps the SDK minimal.

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** the file-based policy source is new surface; tests are authored with the issue. Integration baselines for the pathways the package joins: [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md), [observability-and-silent-failures.md](configuration-test-coverage/pathways/observability-and-silent-failures.md).

---

### Issue 26 - OpAMP-backed telemetry policy source package

**Priority:** P2 **Refs:** [Deep Dive H.1
table](configuration-analysis-deep-dives.md#h1-design-principle---abstractions-in-sdk-implementations-as-opt-in-packages),
[Deep Dive H.4](configuration-analysis-deep-dives.md#h4-opamp-considerations),
[S4.5 Step 16](configuration-analysis.md#45-recommended-build-order) **Depends
on:** Issue 24

Package: `OpenTelemetry.Extensions.TelemetryPolicies.OpAMP` *(name TBD)*

Implement an OpAMP-backed `TelemetryPolicyConfigurationProvider` that translates
OpAMP effective configuration messages into `IConfiguration` key/value pairs.
**Must** be a separate NuGet package - the OpAMP client dependency should never
be forced on consumers who don't use it.

Key constraints:

- Network receive callback must not block - accept new values and return
  immediately; `OnReload()` dispatches asynchronously via the options
  infrastructure
- Expose builder registration (e.g.,
  `AddOpAmpPolicySource(Action<OpAmpPolicyOptions>)`) that wires up the
  `IConfigurationSource` with correct priority ordering
- Consumers with their own OpAMP-based configuration source are free to
  implement their own `IConfigurationProvider` - the SDK's abstractions do not
  prevent this

<!-- markdownlint-disable-next-line MD013 -->
**Baseline tests required:** the OpAMP policy source is new surface; tests are authored with the issue. Integration baselines for the pathways the package joins: [reload-no-op-baseline.md](configuration-test-coverage/pathways/reload-no-op-baseline.md), [observability-and-silent-failures.md](configuration-test-coverage/pathways/observability-and-silent-failures.md).
