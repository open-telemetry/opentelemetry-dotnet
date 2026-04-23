# Delegating Options Factory Priority - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

| File | Lines | Role |
| --- | --- | --- |
| `src/Shared/Options/DelegatingOptionsFactory.cs` | 1-118 | Factory implementation: `Create` execution order |
| `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs` | 1-72 | `RegisterOptionsFactory<T>` overloads; `DisableOptionsReloading<T>` |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs` | 54-59 | Registration: `OtlpExporterOptions`, `ExperimentalOptions`, `SdkLimitOptions` |
| `src/OpenTelemetry/Internal/Builder/ProviderBuilderServiceCollectionExtensions.cs` | 23-56 | Registration: batch processor and metric reader options |
| `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs` | 176 | Registration: named `OtlpExporterBuilderOptions` with `IServiceProvider` factory |

The `Create` method in `DelegatingOptionsFactory<T>` (line 79) executes in this order:

1. `optionsFactoryFunc(configuration, name)` - constructs the options instance; the
   constructor reads env vars and `IConfiguration` keys.
2. `IConfigureOptions<T>` delegates in DI registration order. Named delegates
   (`IConfigureNamedOptions<T>`) apply to every name; plain `IConfigureOptions<T>`
   delegates apply only when `name == Options.DefaultName`.
3. `IPostConfigureOptions<T>` delegates in DI registration order; apply to every name.
4. `IValidateOptions<T>` runs last; throws `OptionsValidationException` if any
   validator returns a failure. Currently zero validators are registered.

## 1. Existing coverage

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:ServiceCollectionAddOpenTelemetryNoParametersTest` | `AddOpenTelemetry`/`UseOpenTelemetry` invoke options callback (Theory: 2) | DI | None (no env vars set) |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:ServiceCollectionAddOpenTelemetryConfigureActionTests` | Multiple `Configure`/`ConfigureAll` calls (Theory: 6) | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:UseOpenTelemetryOptionsOrderingTest` | `Configure<T>` ordering: before-bind / extension / after | DI | None |
| `Trace/TracerProviderBuilderExtensionsTests.cs:ConfigureBuilderIConfigurationAvailableTest` | `IConfiguration` auto-available in `ConfigureBuilder` | DI | `EnvironmentVariableScope` |
| `Trace/TracerProviderBuilderExtensionsTests.cs:ConfigureBuilderIConfigurationModifiableTest` | Custom `IConfiguration` via `ConfigureServices` overrides default | DI | None |
| `Trace/TracerProviderBuilderExtensionsTests.cs:TracerProviderNestedResolutionUsingBuilderTest` | Nested `Configure*` calls and DI scope (Theory: 2) | DI | None |
| `UseOtlpExporterExtensionTests.cs:UseOtlpExporterConfigureTest` | `Configure<T>` delegate with named + unnamed options (Theory) | DI | Class IDisposable + `[Collection("EnvVars")]` |
| `UseOtlpExporterExtensionTests.cs:UseOtlpExporterConfigurationTest` | `IConfiguration`-bound options for all signals, named/unnamed (Theory) | DI | Class IDisposable + `[Collection("EnvVars")]` |

## 2. Scenario checklist and gap analysis

### 2.1 Factory func (lowest precedence; reads env vars / IConfiguration via constructor)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Factory func output is visible in the resulting options before `Configure<T>` runs | Implicit in all env-var + IConfiguration tests | Factory output forms the options baseline | partial |
| Factory func output is overridable by a subsequent `Configure<T>` delegate | `UseOpenTelemetryOptionsOrderingTest`, `UseOtlpExporterConfigureTest` | Correct: `IConfigureOptions<T>` runs after factory | partial |
| Explicitly pin factory-first order with a counter or value set in factory and asserted before configure | No test establishes an explicit before/after priority assertion | Factory always runs first | missing |

### 2.2 `IConfigureOptions<T>` / `Configure<T>` in DI registration order

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Multiple `Configure<T>` calls applied in registration order | `ServiceCollectionAddOpenTelemetryConfigureActionTests` (Theory: 6) | Correct: `_setups` array iterated in order | partial |
| Later `Configure<T>` value wins over earlier one for the same property | `UseOpenTelemetryOptionsOrderingTest` | Correct | covered |
| `Configure<T>` applied after factory overrides factory output | `UseOtlpExporterConfigureTest` | Correct | covered |

### 2.3 `IPostConfigureOptions<T>` / `PostConfigure<T>` in DI registration order

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `PostConfigure<T>` runs after all `Configure<T>` delegates | No test; no `PostConfigure<T>` registrations in the SDK today | Pipeline exists in `DelegatingOptionsFactory`; never exercised | missing |
| `PostConfigure<T>` value wins over `Configure<T>` for the same property | No test | Correct per pipeline ordering | missing |
| `PostConfigure<T>` applies to every named options instance including non-default names | No test | Correct: `post.PostConfigure(name, options)` always called | missing |

### 2.4 `IValidateOptions<T>` / `ValidateOnStart` (highest gate; never wired today)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IValidateOptions<T>` failure throws `OptionsValidationException` | No test; validation pipeline exists but no validators registered | `_validations.Length == 0` so the validation block is never entered | missing |
| `ValidateOnStart` surfaces validation errors at host startup | No test | Not wired | missing |
| Validation runs after all `Configure<T>` and `PostConfigure<T>` delegates | No test | Correct per pipeline ordering | missing |

### 2.5 Named options: `IConfigureNamedOptions<T>` applied to every name

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Named `Configure<T>` delegate applied to the matching named instance | `UseOtlpExporterConfigureTest` (Theory: named + unnamed) | Correct: `namedSetup.Configure(name, options)` | covered |
| Named `Configure<T>` delegate applied to the default-name instance | `UseOtlpExporterConfigureTest` (unnamed path) | Correct | covered |
| Two distinct named instances run independent `Configure<T>` chains | `UseOtlpExporterConfigureTest` | Correct | covered |

### 2.6 Unnamed (default) options: plain `IConfigureOptions<T>` NOT applied to named instances

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Plain `Configure<T>` (not `IConfigureNamedOptions<T>`) skipped for non-default name | No explicit negative-assertion test | `else if (name == Options.DefaultName)` guard in `DelegatingOptionsFactory:89` | missing |
| Plain `Configure<T>` applied when name equals `Options.DefaultName` | Implicit in unnamed-options tests | Correct | partial |

### 2.7 `TryAddSingleton` first-wins semantics

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Pre-registered `IOptionsFactory<T>` silently wins; SDK factory skipped | No test | `TryAddSingleton` no-ops if service already registered | missing |
| SDK factory registers correctly when no prior registration exists | Implicit in all DI-based options tests | Correct | covered |

## 3. Recommendations

### R1: DelegatingOptionsFactory_FactoryFunc_RunsBeforeConfigure

- **Target test name:** `DelegatingOptionsFactory_FactoryFunc_RunsBeforeConfigure`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/DelegatingOptionsFactoryPriorityTests.cs`
  (new file)
- **Tier:** 2
- **Observation mechanism:** DI. Build an `IServiceCollection` with `RegisterOptionsFactory`
  supplying a factory func that sets a known property value; add a `Configure<T>` delegate
  that overwrites it with a different value; resolve `IOptions<T>.Value` and assert the
  `Configure<T>` value is present, confirming `Configure<T>` ran after the factory.
- **Guards issues:** Issue 1, Issue 2
- **Risks pinned:**
  [Risk 1.1](../configuration-analysis-risks.md#11-options-validation-is-completely-absent)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #2 (DelegatingOptionsFactory simplification).
  // Guards risks: Risk 1.1.
  // Observation: DI - resolves options through full factory pipeline.
  // Coverage index: pathway.delegating-options-factory-priority.factory-func.runs-before-configure
  ```
- **Risk vs reward:** Low effort for a high-value anchor. When Issue 2 replaces the fork with a
  `CreateInstance` override, this test verifies the new implementation preserves the same order.

### R2: DelegatingOptionsFactory_PostConfigure_RunsAfterConfigure

- **Target test name:** `DelegatingOptionsFactory_PostConfigure_RunsAfterConfigure`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/DelegatingOptionsFactoryPriorityTests.cs`
  (new file)
- **Tier:** 2
- **Observation mechanism:** DI. Register a `Configure<T>` setting property A to value 1 and a
  `PostConfigure<T>` setting property A to value 2; resolve `IOptions<T>.Value` and assert
  value 2 is present.
- **Guards issues:** Issue 1, Issue 2
- **Risks pinned:**
  [Risk 1.5](../configuration-analysis-risks.md#15-postconfigure-gap-for-fallback-chains-under-reload)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #2 (DelegatingOptionsFactory simplification).
  // Guards risks: Risk 1.5.
  // Observation: DI - resolves options through full factory pipeline.
  // Coverage index: pathway.delegating-options-factory-priority.post-configure.runs-after-configure
  ```
- **Risk vs reward:** Low effort. Issue 5 moves `SdkLimitOptions` fallback cascade to
  `PostConfigure<T>`; this test validates the ordering contract that makes that safe.

### R3: DelegatingOptionsFactory_Validation_ThrowsOnFailure

- **Target test name:** `DelegatingOptionsFactory_Validation_ThrowsOnFailure`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/DelegatingOptionsFactoryPriorityTests.cs`
  (new file)
- **Tier:** 2
- **Observation mechanism:** DI. Register a `ValidateOptions<T>` implementation that always
  fails; assert that resolving `IOptions<T>.Value` throws `OptionsValidationException`.
- **Guards issues:** Issue 1, Issue 2
- **Risks pinned:**
  [Risk 1.1](../configuration-analysis-risks.md#11-options-validation-is-completely-absent),
  [Risk 1.2](../configuration-analysis-risks.md#12-silent-configuration-failure-model-vs-fail-fast)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #1 (Add IValidateOptions<T> for all options classes).
  // Guards risks: Risk 1.1, Risk 1.2.
  // Observation: DI - exercises the validation block in DelegatingOptionsFactory.Create.
  // Coverage index: pathway.delegating-options-factory-priority.validation.throws-on-failure
  ```
- **Risk vs reward:** Medium effort (requires writing a stub validator), but essential.
  The validation code path in `DelegatingOptionsFactory` exists today but is never entered;
  this test is the only way to confirm it works before Issue 1 wires real validators.

### R4: DelegatingOptionsFactory_PlainConfigure_SkippedForNamedInstance

- **Target test name:** `DelegatingOptionsFactory_PlainConfigure_SkippedForNamedInstance`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/DelegatingOptionsFactoryPriorityTests.cs`
  (new file)
- **Tier:** 2
- **Observation mechanism:** DI. Register a plain `Configure<T>` (not named) and a named
  `Configure<T>` for name `"myName"`; resolve both the default instance and the named
  instance; assert the plain delegate ran only for the default instance.
- **Guards issues:** Issue 1, Issue 2
- **Risks pinned:**
  [Risk 1.3](../configuration-analysis-risks.md#13-tryaddsingleton-first-wins---silent-misconfiguration-risk)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour. No planned change.
  // Observation: DI - plain IConfigureOptions<T> applies only to default name.
  // Coverage index: pathway.delegating-options-factory-priority.named-options.plain-configure-skipped
  ```
- **Risk vs reward:** Low effort. Pins the `name == Options.DefaultName` guard at
  `DelegatingOptionsFactory.cs:89`; removal or relaxation of that guard would produce a
  visible delta.

### R5: DelegatingOptionsFactory_TryAddSingleton_FirstRegistrationWins

- **Target test name:** `DelegatingOptionsFactory_TryAddSingleton_FirstRegistrationWins`
- **Target test file:**
  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/DelegatingOptionsFactoryPriorityTests.cs`
  (new file)
- **Tier:** 2
- **Observation mechanism:** DI. Register a custom `IOptionsFactory<T>` implementation before
  calling `RegisterOptionsFactory<T>`; resolve `IOptionsFactory<T>` from the built provider
  and assert it is the pre-registered type, not `DelegatingOptionsFactory<T>`.
- **Guards issues:** Issue 1, Issue 2
- **Risks pinned:**
  [Risk 1.3](../configuration-analysis-risks.md#13-tryaddsingleton-first-wins---silent-misconfiguration-risk)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour. No planned change.
  // Observation: DI - TryAddSingleton first-wins contract.
  // Coverage index: pathway.delegating-options-factory-priority.try-add-singleton.first-wins
  ```
- **Risk vs reward:** Low effort. Documents the first-wins behaviour explicitly so a reviewer
  changing `TryAddSingleton` to `AddSingleton` sees the failure immediately.

## Guards issues

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all options classes.
  R3 pins the validation code path that Issue 1 exercises.
- **Issue 2** - Simplify `DelegatingOptionsFactory<T>` using `CreateInstance` override.
  R1, R2, R3, R4, and R5 all pin the execution order of the current implementation.
  When Issue 2 replaces the fork with a `CreateInstance` subclass, these tests verify
  the new class preserves the same priority ordering.
