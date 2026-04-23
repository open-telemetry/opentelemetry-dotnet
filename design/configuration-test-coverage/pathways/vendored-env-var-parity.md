# Vendored Env Var Parity - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

| File | Lines | Role |
| --- | --- | --- |
| `src/Shared/EnvironmentVariables/EnvironmentVariablesConfigurationProvider.cs` | 1-85 | Vendored provider: `Load`, `Normalize`, prefix match, data dictionary |
| `src/Shared/EnvironmentVariables/EnvironmentVariablesConfigurationSource.cs` | 1-29 | Vendored source: `Build(IConfigurationBuilder)` |
| `src/Shared/EnvironmentVariables/EnvironmentVariablesExtensions.cs` | 1-52 | Vendored extensions: three `AddEnvironmentVariables` overloads |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs` | 58 | Consumer: parameterless constructor |
| `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs` | 29 | Consumer: parameterless constructor |
| `src/OpenTelemetry/Logs/Processor/BatchExportLogRecordProcessorOptions.cs` | 29 | Consumer: parameterless constructor |
| `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs` | 25 | Consumer: parameterless constructor |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs` | 25 | Consumer: parameterless constructor |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs` | 19 | Consumer: parameterless constructor |
| `src/OpenTelemetry/Resources/ResourceBuilderExtensions.cs` | 127 | Consumer: lazy `IConfiguration` from env vars |
| `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs` | 80 | Consumer: DI fallback `IConfiguration` registration |

## 1. Existing coverage

All existing coverage is indirect. No test constructs or asserts on the vendored
`EnvironmentVariablesConfigurationProvider` directly. Every row below exercises the
provider through an options-class parameterless constructor that calls
`new ConfigurationBuilder().AddEnvironmentVariables().Build()`.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `Trace/BatchExportActivityProcessorOptionsTests.cs:BatchExportProcessorOptions_EnvironmentVariableOverride` | `OTEL_BSP_*` env vars override defaults | DirectProperty | Class IDisposable snapshot/restore |
| `Trace/BatchExportActivityProcessorOptionsTests.cs:BatchExportProcessorOptions_InvalidEnvironmentVariableOverride` | Invalid env var falls back to default | DirectProperty | Class IDisposable snapshot/restore |
| `Logs/BatchExportLogRecordProcessorOptionsTests.cs:BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride` | `OTEL_BLRP_*` env vars override defaults | DirectProperty | Class IDisposable snapshot/restore |
| `Logs/BatchExportLogRecordProcessorOptionsTests.cs:BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable` | Programmatic setter takes precedence over env var | DirectProperty | Class IDisposable snapshot/restore |
| `Internal/PeriodicExportingMetricReaderHelperTests.cs:CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar` | `OTEL_METRIC_EXPORT_INTERVAL` env var | DirectProperty | Class IDisposable snapshot/restore |
| `Internal/PeriodicExportingMetricReaderHelperTests.cs:CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar` | `OTEL_METRIC_EXPORT_TIMEOUT` env var | DirectProperty | Class IDisposable snapshot/restore |
| `SdkLimitOptionsTests.cs:SdkLimitOptionsIsInitializedFromEnvironment` | `OTEL_ATTRIBUTE_*`, `OTEL_SPAN_*`, `OTEL_LOGRECORD_*` env vars | DirectProperty | Class IDisposable snapshot/restore |
| `OtlpExporterOptionsTests.cs:OtlpExporterOptions_EnvironmentVariableOverride` | All `OTEL_EXPORTER_OTLP_*` env vars (Theory) | DirectProperty | Class IDisposable + `[Collection("EnvVars")]` |
| `OtlpExporterOptionsTests.cs:OtlpExporterOptions_InvalidEnvironmentVariableOverride` | Invalid env var values rejected; default retained | DirectProperty | Class IDisposable + `[Collection("EnvVars")]` |
| `UseOtlpExporterExtensionTests.cs:UseOtlpExporterRespectsSpecEnvVarsTest` | All `OTEL_EXPORTER_OTLP_*` env vars via DI pipeline | DI | Class IDisposable + `[Collection("EnvVars")]` |

## 2. Scenario checklist and gap analysis

### 2.1 Prefix filtering

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_` prefix: matching var loaded | Implicit; all env-var tests set `OTEL_*` vars and assert options properties | `AddIfNormalizedKeyMatchesPrefix` matches by `_normalizedPrefix` with `OrdinalIgnoreCase` | partial |
| Non-`OTEL_` var absent from resulting `IConfiguration` | No negative-assertion test | Correct per implementation; non-matching keys skipped | missing |
| Empty-prefix path loads all env vars | No test for no-prefix registration (`ProviderBuilderServiceCollectionExtensions:80`) | Used in DI fallback; not exercised by any options test | missing |

### 2.2 Double-underscore to colon translation

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `__` in env var name maps to `:` in `IConfiguration` key | No test exercises `__` in any `OTEL_` var | `Normalize(key)` replaces `__` with `ConfigurationPath.KeyDelimiter` (`:`) | missing |
| Multiple `__` sequences produce a nested section path | No test | String replace is global; correct per implementation | missing |

### 2.3 Case handling

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Key lookup in resulting `IConfiguration` is case-insensitive | Implicit; tests set vars in exact uppercase | `Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)` | partial |
| Lowercase env var name matches `IConfiguration` lookup for same key | No test sets a var in lowercase and asserts match | `OrdinalIgnoreCase` dictionary handles this at the dictionary level | missing |

### 2.4 Empty-string values vs absent variables

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Env var set to `""` is stored as empty string, not treated as absent | No test | `data[key] = value` stores `string.Empty`; `IConfiguration[key]` returns `""` | missing |
| Unset env var is absent from `IConfiguration` (returns `null`) | Implicit in all default-value tests | Correct: key not added to dictionary | partial |

### 2.5 Values containing special characters

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Value containing `=` (e.g., base64 in `OTEL_EXPORTER_OTLP_HEADERS`) | No test | Stored verbatim; `=` in value is not treated as a key delimiter | missing |
| Value containing spaces | No test | Stored verbatim; no trimming applied | missing |

### 2.6 Duplicate key handling

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Two env vars normalise to the same `IConfiguration` key; last-writer wins | No test; `GetEnvironmentVariables()` iteration order is undefined by spec | `data[key] = value` overwrites on collision; order depends on OS enumerator | missing |

### 2.7 Ordering relative to other IConfiguration sources

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Parameterless constructor uses a single-source chain (env vars only) | Implicit; `*_UsingIConfiguration` tests show the separate IConfiguration path | Single-source `ConfigurationBuilder`; no ordering ambiguity | partial |
| DI fallback `IConfiguration` at `ProviderBuilderServiceCollectionExtensions:80` loads all env vars | No test directly exercises this registration | Correct: no-prefix load surfaces all env vars to the DI-injected `IConfiguration` | missing |

## 3. Recommendations

### R1: VendoredProvider_NonOtelPrefix_NotLoaded

- **Target test name:** `VendoredProvider_NonOtelPrefix_NotLoaded`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/VendoredEnvVarProviderTests.cs`
  (new file)
- **Tier:** 1
- **Observation mechanism:** DirectProperty. Build
  `new ConfigurationBuilder().AddEnvironmentVariables("OTEL_").Build()` using the vendored
  extension with a non-`OTEL_` env var in scope; assert `configuration[nonOtelKey]` returns
  `null`. No DI layer needed; this directly probes the provider's prefix filter at lowest cost.
- **Guards issues:** Issue 3
- **Risks pinned:**
  [Risk 1.7](../configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #3 (Replace vendored provider with package dependency).
  // guards Issue 3: when vendored copy is deleted, move this file to regression-only.
  // Guards risks: Risk 1.7.
  // Observation: DirectProperty - brittle only if the OTEL_ prefix constant changes.
  // Coverage index: pathway.vendored-env-var-parity.prefix-filtering.non-otel-var-absent
  ```
- **Risk vs reward:** Minimal effort. Pins the prefix-filter contract that every options
  class relies on; a silent divergence in the upstream package is caught immediately.

### R2: VendoredProvider_DoubleUnderscore_MapsToConfigurationPathDelimiter

- **Target test name:** `VendoredProvider_DoubleUnderscore_MapsToConfigurationPathDelimiter`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/VendoredEnvVarProviderTests.cs`
  (new file)
- **Tier:** 1
- **Observation mechanism:** DirectProperty. Call `Load(IDictionary)` with a key containing `__`;
  assert the resulting `IConfiguration` key uses `:` as the hierarchy delimiter.
- **Guards issues:** Issue 3
- **Risks pinned:**
  [Risk 1.7](../configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #3 (Replace vendored provider with package dependency).
  // guards Issue 3: when vendored copy is deleted, move this file to regression-only.
  // Guards risks: Risk 1.7.
  // Observation: DirectProperty - directly probes Normalize() output.
  // Coverage index: pathway.vendored-env-var-parity.double-underscore.maps-to-colon
  ```
- **Risk vs reward:** Low effort. The `__`->`:` rule is load-bearing for hierarchical
  declarative config (Issue 15); divergence is invisible until a user reports a broken
  subsection key.

### R3: VendoredProvider_EmptyStringValue_StoredAsEmptyNotNull

- **Target test name:** `VendoredProvider_EmptyStringValue_StoredAsEmptyNotNull`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/VendoredEnvVarProviderTests.cs`
  (new file)
- **Tier:** 1
- **Observation mechanism:** DirectProperty. Load a dictionary with an `OTEL_` key whose value is
  `string.Empty`; assert `configuration[key]` returns `""` not `null`.
- **Guards issues:** Issue 3
- **Risks pinned:**
  [Risk 1.7](../configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #3 (Replace vendored provider with package dependency).
  // guards Issue 3: when vendored copy is deleted, move this file to regression-only.
  // Guards risks: Risk 1.7.
  // Observation: DirectProperty - empty string vs null distinction.
  // Coverage index: pathway.vendored-env-var-parity.empty-value.empty-string-stored
  ```
- **Risk vs reward:** Low effort. An upstream change treating empty values as absent would
  silently bypass `TryGet*` helpers and fall through to defaults rather than flagging a
  misconfigured variable.

### R4: VendoredProvider_HeaderValueContainsEquals_StoredVerbatim

- **Target test name:** `VendoredProvider_HeaderValueContainsEquals_StoredVerbatim`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/VendoredEnvVarProviderTests.cs`
  (new file)
- **Tier:** 1
- **Observation mechanism:** DirectProperty. Load a dictionary entry for
  `OTEL_EXPORTER_OTLP_HEADERS` whose value contains `=`; assert `IConfiguration[key]`
  returns the full value verbatim.
- **Guards issues:** Issue 3
- **Risks pinned:**
  [Risk 1.7](../configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #3 (Replace vendored provider with package dependency).
  // guards Issue 3: when vendored copy is deleted, move this file to regression-only.
  // Guards risks: Risk 1.7.
  // Observation: DirectProperty - special character in value.
  // Coverage index: pathway.vendored-env-var-parity.special-chars.equals-in-value
  ```
- **Risk vs reward:** Low effort. OTLP header values may be base64-encoded and contain `=`;
  silent truncation at `=` would break authenticated export without any error signal.

### R5: VendoredProvider_MixedCaseKey_IsFound

- **Target test name:** `VendoredProvider_MixedCaseKey_IsFound`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/VendoredEnvVarProviderTests.cs`
  (new file)
- **Tier:** 1 (dictionary-level contract, platform-independent). A Tier 3 test for
  OS-level case sensitivity on Linux is deferred pending the process-isolation strategy
  decision (entry-doc Sec.4).
- **Observation mechanism:** DirectProperty. Load the provider via `Load(IDictionary)` with a
  key in mixed case; assert that `IConfiguration[uppercaseKey]` returns the value.
  Using the `Load(IDictionary)` overload avoids OS-level env var case behaviour.
- **Guards issues:** Issue 3
- **Risks pinned:**
  [Risk 1.7](../configuration-analysis-risks.md#17-the-sdks-internal-environmentvariablesconfigurationprovider-copy)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #3 (Replace vendored provider with package dependency).
  // guards Issue 3: when vendored copy is deleted, move this file to regression-only.
  // Guards risks: Risk 1.7.
  // Observation: DirectProperty - OrdinalIgnoreCase dictionary contract.
  // Coverage index: pathway.vendored-env-var-parity.case-handling.mixed-case-key
  ```
- **Risk vs reward:** Low effort. Explicitly pins the `OrdinalIgnoreCase` contract; OS-level
  Linux case sensitivity is a separate Tier 3 concern deferred to process-isolation strategy
  selection.

## Guards issues

- **Issue 3** - Replace vendored `EnvironmentVariablesConfigurationProvider` with a package
  dependency. Every test recommended in this file pins a contract that Issue 3 must preserve.
  When Issue 3 lands, the new test file moves to regression-only status and the
  `guards Issue 3` code-comment language should be updated to reflect that.
