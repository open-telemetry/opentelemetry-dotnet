# Reload No-Op Baseline - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

| File | Lines | Role |
| --- | --- | --- |
| `src/Shared/Options/SingletonOptionsManager.cs` | 1-45 | Singleton snapshot for `OpenTelemetryLoggerOptions`; `OnChange` is a no-op |
| `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs` | 60-71 | `DisableOptionsReloading<T>` registers `SingletonOptionsManager<T>` |
| `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggingExtensions.cs` | 153 | Only call site of `DisableOptionsReloading` |
| `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs` | 23-56 | Registers batch and metric-reader options factories; no `OnChange` wiring |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs` | 54-59 | Registers OTLP options factories; no `OnChange` wiring |

Grep for `IOptionsMonitor` and `OnChange` across `src/OpenTelemetry.Extensions.Hosting/`
returns no matches. No hosting extension class subscribes to options change notifications.
The only `OnChange` usage in `src/` is `OpenTelemetryLoggerProvider.cs:35`, which receives
`IOptionsMonitor<OpenTelemetryLoggerOptions>` - and that interface is backed by
`SingletonOptionsManager<T>`, so the callback registered there (if any) is never invoked.

## 1. Existing coverage

The only reload-aware tests in the three projects are listed below. The `MetricsOptions`
tests exercise a real reload path that is part of the Microsoft metrics infrastructure
(`Microsoft.Extensions.Diagnostics.Metrics`), not OTel SDK reload wiring.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:OptionReloadingTest` | `IOptions`/`IOptionsMonitor`/`IOptionsSnapshot` each resolve non-null (Theory: 3) | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:MixedOptionsUsageTest` | All three interfaces return the same instance | DI | None |
| `OpenTelemetryMetricsBuilderExtensionsTests.cs:ReloadOfMetricsViaIConfigurationWithExportCleanupTest` | Real `MetricsOptions` reload via in-memory `IConfiguration` (Theory: 4) | DI + Mock + EventSource | `EnvironmentVariableScope` |
| `OpenTelemetryMetricsBuilderExtensionsTests.cs:ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest` | Same, without export cleanup (Theory: 4) | DI + Mock + EventSource | `EnvironmentVariableScope` |

## 2. Scenario checklist and gap analysis

### 2.1 Options classes that are restart-required today

All in-scope OTel SDK options classes are restart-required. No component subscribes to
`IOptionsMonitor<T>.OnChange`. The table below records the current state; each row is
expected to flip when the corresponding issue lands.

| Options class | Restart-required reason | Status | Expected to change under |
| --- | --- | --- | --- |
| `OpenTelemetryLoggerOptions` | `SingletonOptionsManager` prevents reload entirely; `OnChange` never fires | restart-required | Issue 17 |
| `BatchExportActivityProcessorOptions` | `BatchExportProcessor<T>` reads options at construction; no `OnChange` subscriber | restart-required | Issues 17, 21 |
| `BatchExportLogRecordProcessorOptions` | Same as above | restart-required | Issues 17, 21 |
| `PeriodicExportingMetricReaderOptions` | `PeriodicExportingMetricReader` bakes in timer period; no `OnChange` subscriber | restart-required | Issues 17, 21 |
| `OtlpExporterOptions` (named) | Exporter snapshot baked at construction; no `OnChange` subscriber | restart-required | Issues 17, 23 |
| `OtlpExporterOptions` (unnamed) | Explicitly documented as snapshot-only even after Issue 23 | permanently restart-required | n/a |
| `SdkLimitOptions` | Serializers hold reference to options instance; no `OnChange` subscriber | restart-required | Issues 17, 22 |
| `ExperimentalOptions` | No `OnChange` subscriber | restart-required | n/a (no reload planned) |

### 2.2 Options classes with partial reload support

`MetricsOptions` (from `Microsoft.Extensions.Diagnostics.Metrics`) has live reload support
built into the .NET 8+ metrics runtime. This is Microsoft infrastructure, not OTel SDK
code. The `ReloadOfMetricsViaIConfiguration*` tests exercise this path. No OTel-owned
options class has comparable reload support today.

| Options class | Partial reload notes | Covered by |
| --- | --- | --- |
| `MetricsOptions` | Microsoft infrastructure; `IOptionsMonitor<MetricsOptions>.OnChange` wired by .NET runtime | `ReloadOfMetricsViaIConfigurationWith*Test` |

### 2.3 The two categories of "no-op"

There are two distinct no-op contracts to pin, with different root causes:

**Category A - `SingletonOptionsManager` prevents OnChange from firing:**
Only `OpenTelemetryLoggerOptions`. The `IOptionsMonitor<T>` implementation is
`SingletonOptionsManager<T>`, whose `OnChange` discards the callback immediately.
After `IConfigurationRoot.Reload()`, `CurrentValue` is unchanged (same object reference).

**Category B - Standard `IOptionsMonitor<T>` fires OnChange, but the component ignores it:**
All other in-scope options classes use standard `IOptionsMonitor<T>`. After
`IConfigurationRoot.Reload()`, the options system re-creates options from the updated
`IConfiguration`. The `IOptionsMonitor<T>.CurrentValue` may return a new object.
However, the built component (processor, reader, exporter) holds no subscription and
reads no live options reference; its behaviour is unchanged.

Distinguishing these two categories is important because tests for them are structured
differently:
- Category A: assert `IOptionsMonitor.CurrentValue` is the same reference before and after reload.
- Category B: assert the COMPONENT BEHAVIOUR is unchanged after reload, even though the
  options value may have changed.

### 2.4 Test-setup pattern

The shared setup pattern (used by the `ReloadOfMetricsViaIConfiguration*` tests and
required by all per-class reload-no-op tests) is:

1. Create a `MemoryConfigurationSource` and `MemoryConfigurationProvider`.
2. Wrap in a `ConfigurationRoot` and add to the host via `AddConfiguration(root)`.
3. Build the OTel component (provider, processor, reader, exporter).
4. Optionally resolve `IOptionsMonitor<T>` and subscribe to `OnChange`.
5. Mutate a config key via `memory.Set(key, newValue)`.
6. Call `configuration.Reload()`.
7. Assert the component's observable behaviour is unchanged.

Issue 18 (`TestConfigurationProvider` for reload testing) will add synchronisation
primitives to step 6 so tests can reliably await callback completion before asserting.
Until Issue 18 lands, tests that need to assert OnChange fires (Category B) must use
a `ManualResetEventSlim` or `TaskCompletionSource` in the subscriber.

### 2.5 Observation mechanism by category

Per entry-doc Sec.2, the chosen mechanism is the lowest-effort one whose brittleness
risk is acceptable for the scenario.

| Options class category | Recommended mechanism | Justification |
| --- | --- | --- |
| `OpenTelemetryLoggerOptions` (Category A) | DI (`IOptionsMonitor<T>`) | Reference equality of `CurrentValue`; no reflection needed |
| Batch processors (Category B) | Reflection on `scheduledDelayMilliseconds` field | No public accessor; per-class files must name the exact field |
| Metric reader (Category B) | Reflection on timer period | No public accessor; per-class files must name the exact field |
| OTLP exporter (Category B) | DI + InternalAccessor | Resolve `IOptionsMonitor<OtlpExporterOptions>` for snapshot; InternalAccessor if available for endpoint |

Per-class files under `options/` carry the field-level detail. This file defines the
pattern; it does not repeat per-class specifics.

### 2.6 Existing test gaps

| Scenario | Currently tested by | Status |
| --- | --- | --- |
| Category A: `OpenTelemetryLoggerOptions` value unchanged after `IConfigurationRoot.Reload()` | `OptionReloadingTest` / `MixedOptionsUsageTest` test resolution, not post-reload stability | missing |
| Category A: `OnChange` subscriber never invoked after `IConfigurationRoot.Reload()` | No test | missing |
| Category B: batch processor continues using original interval after reload of `OTEL_BSP_SCHEDULE_DELAY` | No test | missing |
| Category B: metric reader continues using original period after reload of `OTEL_METRIC_EXPORT_INTERVAL` | No test | missing |
| Category B: OTLP exporter ignores reload of `OTEL_EXPORTER_OTLP_ENDPOINT` | No test | missing |
| Shared harness: `MemoryConfigurationSource` + `Reload()` pattern available to all three test projects | `ReloadOfMetricsViaIConfiguration*` sets the pattern in Hosting tests only | partial |

## 3. Recommendations

The tests recommended here are shared-infrastructure tests. They pin the SDK-wide
restart-required contract rather than the behaviour of a single options class.
Per-class reload-no-op tests that follow the pattern set here belong in the per-class
files under `options/`.

### R1: ReloadBaseline_LoggerOptions_CategoryA_OnChangeNotFired

- **Target test name:** `ReloadBaseline_LoggerOptions_CategoryA_OnChangeNotFired`
- **Target test file:** `test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
- **Tier:** 2
- **Observation mechanism:** DI (entry-doc Sec.2.2). Resolve
  `IOptionsMonitor<OpenTelemetryLoggerOptions>`; subscribe a flag-setting callback to
  `OnChange`; trigger `configuration.Reload()`; assert the flag was NOT set and
  `CurrentValue` is reference-equal to the pre-reload snapshot.
- **Guards issues:** Issue 17, Issue 18
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal),
  [Risk 2.3](../configuration-analysis-risks.md#23-onchange-callback-exception-safety),
  [Risk 4.6](../configuration-analysis-risks.md#46-testing-infrastructure-for-reload-scenarios)
- **Code-comment hint:**
  ```
  // BASELINE: pins restart-required contract.
  // Expected to flip under Issue #17 (OnChange subscriber pattern) when
  // reload for OpenTelemetryLoggerOptions lands.
  // Guards risks: Risk 2.2, Risk 2.3, Risk 4.6.
  // Observation: DI - CurrentValue reference equality and OnChange non-invocation.
  // Coverage index: pathway.reload-no-op-baseline.logger-options.on-change-not-fired
  ```
- **Risk vs reward:** Medium effort; requires in-memory `IConfigurationRoot` setup. High
  value: this is the Category A anchor test. Without it, the `SingletonOptionsManager`
  no-op contract is invisible in CI.

### R2: ReloadBaseline_BatchProcessor_CategoryB_ComponentUnchanged

- **Target test name:** `ReloadBaseline_BatchProcessor_CategoryB_ComponentUnchanged`
- **Target test file:** `test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`
- **Tier:** 2
- **Observation mechanism:** Reflection (entry-doc Sec.2.3). Build a `TracerProvider`
  with a `BatchExportActivityProcessorOptions` schedule delay of N milliseconds. After
  `configuration.Reload()` with a new `OTEL_BSP_SCHEDULE_DELAY` value, reflect into the
  processor's `scheduledDelayMilliseconds` field and assert it still equals N.
  Per-class file must name the exact field path once read.
- **Guards issues:** Issue 17, Issue 18, Issue 21
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal),
  [Risk 4.6](../configuration-analysis-risks.md#46-testing-infrastructure-for-reload-scenarios)
- **Code-comment hint:**
  ```
  // BASELINE: pins restart-required contract.
  // Expected to flip under Issue #21 (OnChange for batch export intervals).
  // Guards risks: Risk 2.2, Risk 4.6.
  // Observation: Reflection - brittle under internal field rename; replace with
  // InternalAccessor if a named property is added.
  // Coverage index: pathway.reload-no-op-baseline.batch-processor.component-unchanged
  ```
- **Risk vs reward:** Medium effort (reflection on internal field). Establishes the Category B
  anchor test. When Issue 21 wires `OnChange`, this test is the first to flip.

### R3: ReloadBaseline_MetricReader_CategoryB_ComponentUnchanged

- **Target test name:** `ReloadBaseline_MetricReader_CategoryB_ComponentUnchanged`
- **Target test file:** `test/OpenTelemetry.Tests/Internal/PeriodicExportingMetricReaderHelperTests.cs`
- **Tier:** 2
- **Observation mechanism:** Reflection (entry-doc Sec.2.3). Build a `MeterProvider`
  with `PeriodicExportingMetricReaderOptions` export interval of M milliseconds.
  After `configuration.Reload()` with a new `OTEL_METRIC_EXPORT_INTERVAL`, reflect into
  the reader's timer period and assert it still equals M. Per-class file must name the
  exact field.
- **Guards issues:** Issue 17, Issue 18, Issue 21
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal),
  [Risk 4.6](../configuration-analysis-risks.md#46-testing-infrastructure-for-reload-scenarios)
- **Code-comment hint:**
  ```
  // BASELINE: pins restart-required contract.
  // Expected to flip under Issue #21 (OnChange for metric export intervals).
  // Guards risks: Risk 2.2, Risk 4.6.
  // Observation: Reflection - brittle under internal field rename; replace with
  // InternalAccessor if a named property is added.
  // Coverage index: pathway.reload-no-op-baseline.metric-reader.component-unchanged
  ```
- **Risk vs reward:** Same cost/value profile as R2; together they establish Category B
  coverage across the two primary interval-bearing component types.

### R4: ReloadBaseline_SharedHarness_MemoryConfigReload_Pattern

- **Target test name:** `ReloadBaseline_SharedHarness_MemoryConfigReload_Pattern`
- **Target test file:** `test/OpenTelemetry.Tests/Configuration/ReloadHarnessTests.cs` (new file)
- **Tier:** 1
- **Observation mechanism:** DI (entry-doc Sec.2.2). No OTel components; builds a
  minimal `IServiceCollection` with an `AddInMemoryCollection` source wrapped in a
  `ConfigurationRoot`. Asserts that `IConfigurationRoot.Reload()` after `Set(key, value)`
  changes the value returned by `IConfiguration[key]`. This is a smoke test for the harness
  itself, not for OTel SDK behaviour; it documents the pattern and guards against a
  breakage in the test infrastructure.
- **Guards issues:** Issue 18
- **Risks pinned:**
  [Risk 4.6](../configuration-analysis-risks.md#46-testing-infrastructure-for-reload-scenarios)
- **Code-comment hint:**
  ```
  // BASELINE: pins test harness behaviour. No planned change.
  // Observation: DI - confirms MemoryConfigurationProvider + Reload() pattern works.
  // Coverage index: pathway.reload-no-op-baseline.harness.memory-config-reload
  ```
- **Risk vs reward:** Very low effort. Explicitly documents the reload harness for future
  contributors; prevents silent harness breakage from making reload-no-op tests vacuously pass.

## Guards issues

- **Issue 17** - Design and implement standard `OnChange` subscriber pattern. When Issue 17
  lands and any component wires `OnChange`, R1, R2, or R3 will flip from green to red,
  identifying exactly which component now reacts.
- **Issue 18** - Add `TestConfigurationProvider` for reload testing. R4 is the entry point
  for validating that the new `TestConfigurationProvider` integrates with the shared harness.
  When Issue 18 lands, per-class tests should migrate from `MemoryConfigurationSource` to
  `TestConfigurationProvider` for `OnChange` synchronisation.
- **Issue 19** - `ReloadableSampler` and `IOptionsMonitor<SamplerOptions>` wiring. When
  Issue 19 lands, a new per-class reload test for `SamplerOptions` should be added;
  the harness pattern in R4 applies.
- **Issue 20** - Export enable/disable kill-switch via `OnChange` in `BatchExportProcessor`.
  When Issue 20 lands, R2 flips for that property.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals. When Issue 21
  lands, R2 and R3 flip.
- **Issue 22** - Wire `OnChange` for `SdkLimitsOptions`. When Issue 22 lands, a
  `SdkLimitOptions`-specific reload test (following the pattern in R2) should be added.
- **Issue 23** - OTLP exporter reload. When Issue 23 lands, a named-options reload test
  for `OtlpExporterOptions` should be added, following the Category B pattern in R2.
