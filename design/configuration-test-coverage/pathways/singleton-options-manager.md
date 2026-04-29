# Singleton Options Manager - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

| File | Lines | Role |
| --- | --- | --- |
| `src/Shared/Options/SingletonOptionsManager.cs` | 1-45 | Full class: `IOptionsMonitor<T>`, `IOptionsSnapshot<T>`, `NoopChangeNotification` |
| `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs` | 60-71 | `DisableOptionsReloading<T>`: registers `SingletonOptionsManager<T>` as monitor and snapshot |
| `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggingExtensions.cs` | 153 | Only call site: `services.DisableOptionsReloading<OpenTelemetryLoggerOptions>()` |
| `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerProvider.cs` | 35 | Consumer: `IOptionsMonitor<OpenTelemetryLoggerOptions>` injected in constructor |
| `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs` | 13 | Options class whose DI registration uses `SingletonOptionsManager` |

Key facts from the source:

- `SingletonOptionsManager<T>` implements `IOptionsMonitor<T>` and `IOptionsSnapshot<T>`,
  but NOT `IOptions<T>`.
- It is constructed with `IOptions<T>` from DI. Its constructor stores `options.Value`
  (line 21) as `this.instance`, which is the frozen options snapshot.
- `CurrentValue`, `Value`, and `Get(string? name)` all return `this.instance`
  regardless of the name argument.
- `OnChange(Action<TOptions, string?> listener)` returns `NoopChangeNotification.Instance`
  (line 31). The listener is never stored and never invoked.
- `DisableOptionsReloading<T>()` uses `TryAddSingleton` and `TryAddScoped`, so the
  registration only applies when no prior `IOptionsMonitor<T>` or `IOptionsSnapshot<T>`
  is registered.
- Today the only consumer is `OpenTelemetryLoggerOptions`. No other options class in the
  in-scope packages calls `DisableOptionsReloading<T>`.

## 1. Existing coverage

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:OptionReloadingTest` | `IOptions`/`IOptionsMonitor`/`IOptionsSnapshot` each resolve non-null (Theory: 3) | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:MixedOptionsUsageTest` | `IOptions`, `IOptionsMonitor`, `IOptionsSnapshot` return the same instance | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:UseOpenTelemetryOptionsOrderingTest` | `Configure<T>` ordering: before-bind / extension / after-bind | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:ServiceCollectionAddOpenTelemetryNoParametersTest` | `AddOpenTelemetry`/`UseOpenTelemetry` invoke options callback (Theory: 2) | DI | None |
| `Logs/OpenTelemetryLoggingExtensionsTests.cs:ServiceCollectionAddOpenTelemetryConfigureActionTests` | Multiple `Configure`/`ConfigureAll` calls (Theory: 6) | DI | None |

## 2. Scenario checklist and gap analysis

### 2.1 Post-build `Configure<T>` silent no-op

The options snapshot is frozen when `IOptions<T>` is first resolved by the DI container
(during provider build). A `Configure<T>` call made to the `IServiceCollection` before
the service provider is built IS applied, because `DelegatingOptionsFactory<T>` captures
`sp.GetServices<IConfigureOptions<T>>()` at its own construction time. A call made after
the provider is built has no effect.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Configure<T>` calls made before provider build are applied | `UseOpenTelemetryOptionsOrderingTest`, `ServiceCollectionAddOpenTelemetryConfigureActionTests` | Correct | covered |
| `Configure<T>` call made after provider is built has no effect | No test explicitly mutates options after build and asserts no change | `DelegatingOptionsFactory<T>` is a singleton; captures `IConfigureOptions<T>` at construction time; post-build additions are not seen | missing |

### 2.2 `IOptionsMonitor<T>.OnChange` never fires

`OnChange` returns `NoopChangeNotification.Instance`. The listener `Action` is never stored
and is never called, regardless of `IConfigurationRoot.Reload()` or any programmatic change.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OnChange` subscriber is never called after `IConfigurationRoot.Reload()` | No test; `MixedOptionsUsageTest` does not subscribe to `OnChange` | Correct: `NoopChangeNotification` returned; listener discarded | missing |
| `OnChange` returns a non-null `IDisposable` token (safe to dispose) | No test | `NoopChangeNotification.Instance` is returned; `Dispose()` is a no-op | missing |

### 2.3 `IOptionsSnapshot<T>` returns the singleton for every name

`IOptionsSnapshot<T>` is typically scoped and name-aware. Under `SingletonOptionsManager<T>`,
`Get(name)` returns `this.instance` for every name, including non-default names.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsSnapshot<T>.Get(Options.DefaultName)` returns the singleton | `MixedOptionsUsageTest` (all three return same instance) | Correct | covered |
| `IOptionsSnapshot<T>.Get("nonDefaultName")` returns the same singleton | No test | Correct: `Get(string? name)` ignores the name argument | missing |
| `IOptionsSnapshot<T>.Value` returns the singleton | `MixedOptionsUsageTest` | Correct | covered |

### 2.4 `IOptionsMonitor<T>.CurrentValue` unchanged after `IConfigurationRoot.Reload()`

`CurrentValue` always returns `this.instance`, which is set once in the constructor and
never updated.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `CurrentValue` is unchanged after `IConfigurationRoot.Reload()` | No test exercises reload against `OpenTelemetryLoggerOptions` | Correct: `this.instance` is immutable | missing |
| `CurrentValue` is unchanged after a programmatic `Configure<T>` call post-build | No test | Correct | missing |

## 3. Recommendations

### R1: SingletonOptionsManager_OnChange_CallbackNeverFired

- **Target test name:** `SingletonOptionsManager_OnChange_CallbackNeverFired`
- **Target test file:** `test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
- **Tier:** 2
- **Observation mechanism:** DI. Build the service provider; resolve
  `IOptionsMonitor<OpenTelemetryLoggerOptions>`; subscribe to `OnChange` with a flag-setting
  callback; trigger `IConfigurationRoot.Reload()` on an in-memory configuration; assert the
  callback was NOT invoked and the options `Value` is unchanged. Requires an in-memory
  `IConfigurationRoot` added to the service collection.
- **Guards issues:** Issue 17
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal),
  [Risk 2.3](../configuration-analysis-risks.md#23-onchange-callback-exception-safety)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #17 (standard OnChange subscriber pattern).
  // Guards risks: Risk 2.2, Risk 2.3.
  // Observation: DI - resolves IOptionsMonitor, subscribes to OnChange, fires Reload().
  // Coverage index: pathway.singleton-options-manager.on-change.callback-never-fired
  ```
- **Risk vs reward:** Medium effort. This is the key test that breaks if `SingletonOptionsManager`
  is replaced with a live `IOptionsMonitor` wiring under Issue 17; without it, the
  behaviour change is invisible in CI.

### R2: SingletonOptionsManager_CurrentValue_UnchangedAfterReload

- **Target test name:** `SingletonOptionsManager_CurrentValue_UnchangedAfterReload`
- **Target test file:** `test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
- **Tier:** 2
- **Observation mechanism:** DI. Capture `IOptionsMonitor<OpenTelemetryLoggerOptions>.CurrentValue`
  before and after `IConfigurationRoot.Reload()`; assert both references are equal (same
  object) and that modified configuration keys do not appear in the post-reload value.
- **Guards issues:** Issue 17
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour.
  // Expected to change under Issue #17 (standard OnChange subscriber pattern).
  // Guards risks: Risk 2.2.
  // Observation: DI - reference equality of CurrentValue across Reload().
  // Coverage index: pathway.singleton-options-manager.current-value.unchanged-after-reload
  ```
- **Risk vs reward:** Low effort once the in-memory `IConfigurationRoot` setup from R1 exists.
  Pins the snapshot-after-build contract; any reload wiring that reaches this options class
  produces a visible failure.

### R3: SingletonOptionsManager_SnapshotGet_NonDefaultName_ReturnsSingleton

- **Target test name:** `SingletonOptionsManager_SnapshotGet_NonDefaultName_ReturnsSingleton`
- **Target test file:** `test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
- **Tier:** 2
- **Observation mechanism:** DI. Resolve `IOptionsSnapshot<OpenTelemetryLoggerOptions>`;
  call `Get("some-non-default-name")`; assert it returns the same reference as `Value` and
  as `IOptionsMonitor<OpenTelemetryLoggerOptions>.CurrentValue`.
- **Guards issues:** Issue 17
- **Risks pinned:**
  [Risk 2.2](../configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal)
- **Code-comment hint:**
  ```
  // BASELINE: pins current behaviour. No planned change.
  // Observation: DI - Get(nonDefaultName) returns singleton for IOptionsSnapshot.
  // Coverage index: pathway.singleton-options-manager.snapshot.non-default-name-returns-singleton
  ```
- **Risk vs reward:** Low effort. Explicitly pins that `IOptionsSnapshot<T>.Get(name)` ignores
  its name argument; a future refactor restoring name-aware scoped behaviour would cause a
  visible delta here.

## Guards issues

- **Issue 17** - Design and implement standard `OnChange` subscriber pattern. If
  `SingletonOptionsManager` is replaced with a live `IOptionsMonitor` wiring for
  `OpenTelemetryLoggerOptions`, R1 and R2 are the first tests to break.
