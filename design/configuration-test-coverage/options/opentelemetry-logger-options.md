# OpenTelemetryLoggerOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:13`.
- Public properties:
  - `IncludeFormattedMessage` (default `false`) -
    `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:29`.
  - `IncludeScopes` (default `false`) -
    `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:36`.
  - `ParseStateValues` (default `false`) -
    `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:63`.
- Internal properties:
  - `IncludeAttributes` (default `true`) -
    `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:70`.
  - `IncludeTraceState` (default `false`) -
    `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:78`.
- Internal mutable state: `ProcessorFactories` list and `ResourceBuilder`
  field (not configuration-bound properties) -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:15-16`.
- `Copy()` internal method (used at provider construction to snapshot the
  options; this is the live instance passed to each `OpenTelemetryLogger`) -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:127-134`.
- No env-var reads: `OpenTelemetryLoggerOptions` has no env-var constructor.
  Configuration binding is applied via
  `LoggerProviderOptions.RegisterProviderOptions<OpenTelemetryLoggerOptions, OpenTelemetryLoggerProvider>`
  at `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggingExtensions.cs:254`,
  which binds the `Logging:OpenTelemetry` section of the host
  `IConfiguration` to the options instance through the standard
  Microsoft.Extensions.Logging pipeline.

### SingletonOptionsManager registration

The call that opts this class out of reload is:

```text
services.DisableOptionsReloading<OpenTelemetryLoggerOptions>()
```

at `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggingExtensions.cs:153`
inside `AddOpenTelemetryInternal`.

`DisableOptionsReloading<T>` is defined at
`src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs:60-71`.
It calls `TryAddSingleton<IOptionsMonitor<T>, SingletonOptionsManager<T>>`
(line 67) and `TryAddScoped<IOptionsSnapshot<T>, SingletonOptionsManager<T>>`
(line 68).

`SingletonOptionsManager<T>` is defined at
`src/Shared/Options/SingletonOptionsManager.cs:11-45`. Its constructor
resolves `IOptions<T>.Value` once (line 21) and stores it as the fixed
`instance`. `Get(name)` (line 28), `CurrentValue` (line 24), and
`Value` (line 26) all return that single instance regardless of the name
argument. `OnChange` (line 30) returns `NoopChangeNotification.Instance`
(a no-op `IDisposable`) and never fires its listener.

### Direct consumer sites

`OpenTelemetryLoggerProvider` reads `options.CurrentValue` once during
construction at line 40 and copies the result into `this.Options` via
`options.Copy()` at line 59
(`src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerProvider.cs:35-61`).
The copy is passed to each `OpenTelemetryLogger` instance.

`OpenTelemetryLogger.Log` reads four option properties on every log call:

- `options.IncludeTraceState` -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLogger.cs:63`.
- `options.IncludeScopes` -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLogger.cs:68`.
- `options.IncludeAttributes` and `options.ParseStateValues` -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLogger.cs:80`.
- `options.IncludeFormattedMessage` -
  `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLogger.cs:92`.

All four reads operate against the `Copy()` snapshot taken during provider
construction, not the live DI-registered options instance. A post-build
`Configure<OpenTelemetryLoggerOptions>` call therefore has no effect on
already-created loggers.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

All tests are in
`test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
(abbreviated `OTLE`).

The inventory row in entry-doc Section 1.2 credits 7 tests. Counting the
`OpenTelemetryLoggerOptions`-tagged rows in `existing-tests.md` Sec.1.A
confirms exactly 7: `ServiceCollectionAddOpenTelemetryNoParametersTest`
(Theory x2), `ServiceCollectionAddOpenTelemetryConfigureActionTests`
(Theory x6), `UseOpenTelemetryDependencyInjectionTest`,
`UseOpenTelemetryOptionsOrderingTest`,
`TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions`,
`OptionReloadingTest` (Theory x3), `MixedOptionsUsageTest`. The Theory
tests are counted as one method entry each; 7 methods in total.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OTLE.ServiceCollectionAddOpenTelemetryNoParametersTest` | `Configure<T>` callback fires when provider is built; `AddOpenTelemetry` vs `UseOpenTelemetry` are equivalent (Theory x2) | DI (`ILoggerFactory` construction side-effect; no explicit options-property assertion) | Not env-var dependent |
| `OTLE.ServiceCollectionAddOpenTelemetryConfigureActionTests` | Multiple `Configure`/`ConfigureAll` calls; callback invocation count; single options instance asserted via reference equality (Theory x6) | DI + reference equality on `OpenTelemetryLoggerOptions` | Not env-var dependent |
| `OTLE.UseOpenTelemetryDependencyInjectionTest` | `ConfigureServices` + `ConfigureBuilder` DI composition; processor reaches `LoggerProviderSdk.Processor` | InternalAccessor (`LoggerProviderSdk.Processor`) | Not env-var dependent |
| `OTLE.UseOpenTelemetryOptionsOrderingTest` | `Configure<T>` ordering: pre-bind delegate fires first; then `Logging:OpenTelemetry` IConfiguration binding; then extension delegate; then post-bind delegate | DI + `IConfiguration` in-memory collection; ordering asserted via index counter; `IncludeFormattedMessage` directly asserted | Not env-var dependent |
| `OTLE.TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` | All properties on `OpenTelemetryLoggerOptions` are of primitive type (AOT/trim safety) | Reflection (`typeof(OpenTelemetryLoggerOptions).GetProperties(...)`) | Not env-var dependent |
| `OTLE.OptionReloadingTest` | `IOptionsMonitor` / `IOptionsSnapshot` / plain access all return the same singleton; `IConfigurationRoot.Reload()` does not re-invoke the configure delegate (Theory x3) | DI (`IOptionsMonitor<OpenTelemetryLoggerOptions>`, `IOptionsSnapshot<OpenTelemetryLoggerOptions>`); delegate invocation count | Not env-var dependent |
| `OTLE.MixedOptionsUsageTest` | `IOptions`, `IOptionsMonitor`, `IOptionsSnapshot` all return the same object reference (singleton identity) | DI + `ReferenceEquals` | Not env-var dependent |

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Constructor / env-var binding

`OpenTelemetryLoggerOptions` has no env-var constructor. Configuration is
applied through the `Logging:OpenTelemetry` `IConfiguration` section bound
by `RegisterProviderOptions` at provider registration time.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IncludeFormattedMessage` from `Logging:OpenTelemetry:IncludeFormattedMessage` in `IConfiguration` | `UseOpenTelemetryOptionsOrderingTest` (asserts `true` after bind; initial value is `false`) | `IConfiguration` binding via `RegisterProviderOptions`; `true`/`false` parsed | covered |
| `IncludeScopes` from `Logging:OpenTelemetry:IncludeScopes` in `IConfiguration` | - | Same binding path | missing |
| `ParseStateValues` from `Logging:OpenTelemetry:ParseStateValues` in `IConfiguration` | - | Same binding path | missing |
| No env-var binding path for any property (confirmed: no `OTEL_*` var reads) | - | N/A by design | n/a |

Note: the `RegisterProviderOptions` path means `IConfiguration` is the
primary external source (there is no env-var alternative). The binding
section key is `Logging:OpenTelemetry`.

### 2.2 Priority order

The target priority for this class is: programmatic `Configure<T>` /
extension-method callback > `Logging:OpenTelemetry` IConfiguration binding
> type default. There is no signal-specific env-var fallback and no
`DelegatingOptionsFactory` in this pathway.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Configure<T>` delegate ordering relative to `IConfiguration` binding (pre-bind vs post-bind) | `UseOpenTelemetryOptionsOrderingTest` | Pre-bind `Configure<T>` fires first; `IConfiguration` binding then overwrites; extension delegate fires after | covered |
| Extension-method configure callback beats `IConfiguration` when both present | `UseOpenTelemetryOptionsOrderingTest` (extension callback fires after bind, so it wins) | Extension callback wins over `IConfiguration` | covered |
| Type default (`false` for all bool properties) observed when no `IConfiguration` or `Configure<T>` is present | Implicit in `ServiceCollectionAddOpenTelemetryConfigureActionTests` (options instance checked for identity, not values) | All `false` at construction | partial (identity asserted, default values not directly verified) |
| `Configure<T>` registered after `AddOpenTelemetry` fires in correct order | `ServiceCollectionAddOpenTelemetryConfigureActionTests` (counts invocations; does not assert values) | Fires; invocation order is after `IConfiguration` binding | partial (count only; effective value not asserted) |
| Post-build `Configure<T>` call has no effect on already-constructed `OpenTelemetryLogger` instances (singleton semantics) | `OptionReloadingTest` (indirectly: reload does not re-invoke delegate) | `SingletonOptionsManager` holds the options snapshot; post-build delegates are silently ignored on reload | partial (reload is covered; direct post-build Configure call after provider construction is not tested) |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All three public properties at their type defaults (`IncludeFormattedMessage = false`, `IncludeScopes = false`, `ParseStateValues = false`) | - | Type defaults are `false` for all three booleans | missing |
| Stable snapshot of the full default shape (including internal properties `IncludeAttributes = true`, `IncludeTraceState = false`) | - | Not snapshotted | missing (candidate for snapshot-library pilot) |

### 2.4 Named-options subsection

N/A - single instance. `DisableOptionsReloading<OpenTelemetryLoggerOptions>`
registers `SingletonOptionsManager<OpenTelemetryLoggerOptions>` as
`IOptionsMonitor` and `IOptionsSnapshot`. `SingletonOptionsManager.Get(name)`
ignores `name` and always returns the same instance. There are no
named instances of this class. `MixedOptionsUsageTest` already verifies
singleton identity across `IOptions`, `IOptionsMonitor`, and
`IOptionsSnapshot`.

### 2.5 Invalid-input characterisation

This class has no numeric or URI properties. Inputs are three public
`bool` properties and two internal `bool` properties. Invalid-input
scenarios are narrow.

| Property | Malformed input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `IncludeFormattedMessage` | `IConfiguration` value `"invalid"` (non-boolean string) | `IConfigurationBinder` throws `InvalidOperationException` at bind time; provider build fails | - | missing (silent-fail or exception; current behaviour unverified) |
| `IncludeScopes` | `IConfiguration` value `"invalid"` | Same | - | missing |
| `ParseStateValues` | `IConfiguration` value `"invalid"` | Same | - | missing |
| `AddProcessor` | `null` processor | `Guard.ThrowIfNull` throws `ArgumentNullException` at `AddProcessor` line 88 | `VerifyExceptionIsThrownWhenImplementationFactoryIsNull` (tests null implementation factory; not null `BaseProcessor` directly) | partial (null factory covered; null direct processor argument not covered) |
| `SetResourceBuilder` | `null` resource builder | `Guard.ThrowIfNull` throws `ArgumentNullException` at `SetResourceBuilder` line 120 | - | missing |

All boolean invalid-input rows are expected to change under Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart`). The exact behaviour on
malformed boolean `IConfiguration` input (exception vs silent fallback to
type default) should be pinned.

### 2.6 Reload no-op baseline

This class is the primary user of the `SingletonOptionsManager` pathway.
Reload is suppressed by design - `SingletonOptionsManager.OnChange` is a
no-op. However, this is specifically the behaviour the pathway file
[`../pathways/singleton-options-manager.md`](../pathways/singleton-options-manager.md)
will characterise in full. The table below pins per-property consumer-level
effects.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` does not re-invoke `Configure<T>` delegate | `OptionReloadingTest` | Delegate invocation count stays at 1 after `Reload()` | covered |
| `IOptionsMonitor<OpenTelemetryLoggerOptions>.OnChange` returns a no-op `IDisposable` and the registered listener is never called | - | `SingletonOptionsManager.OnChange` returns `NoopChangeNotification.Instance`; listener never fires | missing |
| `IConfigurationRoot.Reload()` does not change `IncludeFormattedMessage` on an already-constructed `OpenTelemetryLogger` | - | `Copy()` snapshot in `OpenTelemetryLoggerProvider` is captured at build time; reload cannot reach the logger's field | missing |
| `IConfigurationRoot.Reload()` does not change `IncludeScopes` on an already-constructed logger | - | Same | missing |
| `IConfigurationRoot.Reload()` does not change `ParseStateValues` on an already-constructed logger | - | Same | missing |
| Post-build `Configure<OpenTelemetryLoggerOptions>` call does not affect `OpenTelemetryLogger` options (singleton semantics guard) | - | `SingletonOptionsManager` caches the instance resolved at first access; subsequent `Configure<T>` registrations would apply to the live options instance but the logger already holds a `Copy()` | missing |

The last row and the per-property rows are expected to flip under Issue 17
(standard `OnChange` subscriber pattern) once a reload path is introduced.
The `OptionReloadingTest` covers only the delegate-invocation aspect; the
consumer-observable side (logger field values) is untested.

### 2.7 Consumer-observed effects

`OpenTelemetryLogger.Log` reads all four option properties on every log
call. These are the only meaningful consumer effects for this class.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IncludeFormattedMessage = true` -> `LogRecord.FormattedMessage` is populated | - | `iloggerData.FormattedMessage` is set when `IncludeFormattedMessage` is `true` (line 92 of `OpenTelemetryLogger.cs`) | missing |
| `IncludeFormattedMessage = false` -> `LogRecord.FormattedMessage` is `null` when message template present | - | `iloggerData.FormattedMessage` is `null` (line 88 vs 92 branch) | missing |
| `IncludeScopes = true` -> `LogRecord.Attributes` includes active scopes | - | `iloggerData.ScopeProvider` is set to the provider (line 68); scope processor runs | missing |
| `IncludeScopes = false` -> `LogRecord.Attributes` excludes scopes | - | `iloggerData.ScopeProvider` is `null` | missing |
| `ParseStateValues = true` -> log state is parsed into `LogRecord.Attributes`; `LogRecord.State` is `null` | - | `ProcessState` uses the `parseStateValues` flag; `State` is set to `null` when `true` (line 159, 168, 196) | missing |
| `ParseStateValues = false` -> `LogRecord.State` is preserved for legacy exporters | - | `iloggerData.State` is set to the original state object | missing |

All consumer-effect rows are missing. None of the 7 existing tests emit a
log record and assert on `LogRecord` field values.

---

## 3. Recommendations

One bullet per gap. Each recommendation targets a reviewable PR unit.
Test name follows the dominant `Subject_Condition_Expected` convention.
Target location is the existing test file unless noted. Tier and
observation-mechanism labels per entry-doc Sections 2 and 3.

Rows are grouped by theme; within each theme ordering is from lowest
brittleness to highest.

### 3.1 Default-state baseline

1. **`OpenTelemetryLoggerOptions_Defaults`** (new test in
   `OpenTelemetryLoggingExtensionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs `new
     OpenTelemetryLoggerOptions()` directly and asserts
     `IncludeFormattedMessage == false`, `IncludeScopes == false`,
     `ParseStateValues == false`. Optionally asserts internal properties
     `IncludeAttributes == true`, `IncludeTraceState == false` via
     `InternalsVisibleTo` (already wired from `OpenTelemetry` to
     `OpenTelemetry.Tests` - Session 0a Sec.4.G).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins type defaults. No planned change
     under current issues; update comment if Issue 1 adds `ValidateOnStart`
     for this class."
   - Risk vs reward: trivial; makes gap in Sec.2.3 visible.

2. **`OpenTelemetryLoggerOptions_Default_Snapshot`** (new; same file or
   dedicated `Snapshots/` subfolder per the snapshot-library decision in
   entry-doc Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD by maintainers). This class
     has only primitive properties and is the candidate cited in
     `TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions`; it is a natural
     snapshot pilot.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins whole-options shape. Snapshot update
     expected on any additive change; reviewer confirms intent."
   - Risk vs reward: low per-test cost once the library is chosen; high value
     for catching silent default drift.

### 3.2 IConfiguration binding coverage

1. **`OpenTelemetryLoggerOptions_IncludeScopes_BoundFromIConfiguration`**
   (new; `OpenTelemetryLoggingExtensionsTests.cs`).
   - Tier 2. Mechanism: DI + DirectProperty (resolve
     `IOptions<OpenTelemetryLoggerOptions>` after registering an in-memory
     `IConfiguration` with `Logging:OpenTelemetry:IncludeScopes = true`).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins `IConfiguration` binding for
     `IncludeScopes`. No planned change."
   - Risk vs reward: low brittleness; closes the binding-coverage gap.

2. **`OpenTelemetryLoggerOptions_ParseStateValues_BoundFromIConfiguration`**
   (new; same file). Tier 2. Mechanism: DI + DirectProperty. Same pattern
   as above for `ParseStateValues`. Guards Issue 1.

3. **`OpenTelemetryLoggerOptions_IConfiguration_InvalidBoolean_Behaviour`**
   (new; same file). Tier 2. Mechanism: DI + Exception / DirectProperty.
   Supplies `Logging:OpenTelemetry:IncludeFormattedMessage = "invalid"` in
   the in-memory collection and asserts the current behaviour (either a
   thrown `InvalidOperationException` at bind time or silent fallback to
   `false`). Guards Issue 1.
   - Code-comment hint: "BASELINE: pins current silent-accept or exception
     behaviour for malformed boolean. Expected to change under Issue 1
     (`IValidateOptions<T>` + `ValidateOnStart`)."

### 3.3 Priority-order gaps

1. **`OpenTelemetryLoggerOptions_Configure_AfterIConfiguration_Wins`** (new;
   `OpenTelemetryLoggingExtensionsTests.cs`). Tier 2. Mechanism: DI +
   DirectProperty. Registers an in-memory `IConfiguration` with
   `IncludeFormattedMessage = true`, then registers a `Configure<T>` delegate
   that sets it to `false`. Resolves
   `IOptions<OpenTelemetryLoggerOptions>.Value` and asserts `false`.
   Complementary to `UseOpenTelemetryOptionsOrderingTest` which asserts on
   delegate ordering via index; this test asserts the effective value.
   - Guards Issue 1, Issue 17.
   - Risk vs reward: low brittleness; pins the value-level priority order that
     the ordering test only implies.

2. **`OpenTelemetryLoggerOptions_Defaults_ObservedViaDi`** (new; same file).
   Tier 2. Mechanism: DI. Registers `AddOpenTelemetry()` with no
   configuration, resolves `IOptions<OpenTelemetryLoggerOptions>.Value`, and
   asserts all three public properties are `false`. Closes the gap noted in
   Sec.2.2 where type defaults are implied but never directly asserted through
   the DI pipeline.
   - Guards Issue 1.
   - Risk vs reward: minimal effort; makes the DI default-state baseline
     explicit.

### 3.4 SingletonOptionsManager / reload no-op baseline

These tests characterise the singleton-manager semantics that are unique to
this class. The shared pathway file
[`../pathways/singleton-options-manager.md`](../pathways/singleton-options-manager.md)
will specify the full pattern; the tests below are the `OpenTelemetryLoggerOptions`-
specific instances.

1. **`OpenTelemetryLoggerOptions_OnChange_ReturnsNoopDisposable`** (new;
   `OpenTelemetryLoggingExtensionsTests.cs`). Tier 2. Mechanism: DI
   (`IOptionsMonitor<OpenTelemetryLoggerOptions>.OnChange`). Registers a
   listener, confirms the returned `IDisposable` is non-null, triggers
   `IConfigurationRoot.Reload()`, and asserts the listener was never called.
   - Guards Issue 17.
   - Code-comment hint: "BASELINE: pins no-op `OnChange` under
     `SingletonOptionsManager`. Expected to flip under Issue 17 (standard
     `OnChange` subscriber pattern) when this class gains a reload path."
   - Risk vs reward: low effort; directly characterises the `OnChange`
     no-op that is the key difference between this class and classes using
     `DelegatingOptionsFactory`.

2. **`OpenTelemetryLoggerOptions_SingletonIdentity_AcrossAllInterfaces`**
   (new; same file). Tier 2. Mechanism: DI + `ReferenceEquals`. Resolves
   `IOptions<T>.Value`, `IOptionsMonitor<T>.CurrentValue`,
   `IOptionsMonitor<T>.Get(Options.DefaultName)`,
   `IOptionsMonitor<T>.Get("any-name")`, and `IOptionsSnapshot<T>.Value`
   (scoped) and asserts all five are the same object. `MixedOptionsUsageTest`
   covers three of the five; this extends it to named `Get` calls.
   - Guards Issue 17.
   - Code-comment hint: "BASELINE: pins singleton identity. Under Issue 17
     the named-`Get` behaviour may change if named-options support is added."
   - Risk vs reward: low effort; pins the `Get(name)` no-op which is not
     covered by existing tests.

3. **`OpenTelemetryLoggerOptions_Reload_DoesNotChange_IncludeFormattedMessage_OnLogger`**
   (new; `OpenTelemetryLoggingExtensionsTests.cs`). Tier 2. Mechanism: Mock
   exporter (`InMemoryExporter<LogRecord>` or `TestLogProcessor` already
   available in the project). Registers `IncludeFormattedMessage = false`
   via `IConfiguration`, builds the provider, logs a record with a template,
   calls `IConfigurationRoot.Reload()` after mutating the source to `true`,
   logs again, and asserts both records have `FormattedMessage == null`
   (i.e. the logger's `Copy()` snapshot did not change).
   - Guards Issue 17.
   - Code-comment hint: "BASELINE: pins that `Copy()` snapshot prevents
     reload from affecting live loggers. Expected to flip under Issue 17."
   - Risk vs reward: moderate effort (requires a mock log consumer); high
     value - this is the consumer-visible effect that reload would change.

4. **`OpenTelemetryLoggerOptions_PostBuild_Configure_Has_No_Effect_On_Logger`**
   (new; same file). Tier 2. Mechanism: Mock (`TestLogProcessor` / inline
   processor) + InternalAccessor. Builds the provider, then calls
   `serviceProvider.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>()`
   and mutates the returned instance's `IncludeFormattedMessage` to `true`.
   Logs a record and asserts `FormattedMessage` is still `null`. This directly
   pins the "post-build Configure is a silent no-op" contract unique to this
   class.
   - Guards Issues 17, Issue 1 (validation that could warn on this pattern).
   - Code-comment hint: "BASELINE: pins that mutations to the live
     `IOptions<OpenTelemetryLoggerOptions>` instance after provider
     construction are silently ignored. Expected to change under Issue 17."
   - Risk vs reward: moderate; the highest-value test in this section because
     it directly reproduces the silent-failure mode described in the entry-doc
     `singleton-options-manager.md` pathway reference.

### 3.5 Consumer-observed effects currently missing

All tests below use a mock/in-process log consumer (a processor that captures
`LogRecord` instances) and assert on `LogRecord` field values.

1. **`OpenTelemetryLoggerOptions_IncludeFormattedMessage_True_PopulatesFormattedMessage`**
   (new; `OpenTelemetryLoggingExtensionsTests.cs`). Tier 2. Mechanism: Mock
   (`TestLogProcessor` collects records). Registers `IncludeFormattedMessage =
   true`, emits a log with a template format string, asserts
   `record.ILoggerData.FormattedMessage != null`. Guards Issue 1.

2. **`OpenTelemetryLoggerOptions_IncludeFormattedMessage_False_NoFormattedMessage`**
   (new; same file). Tier 2. Mechanism: Mock. Default (`false`); same log
   call; asserts `FormattedMessage == null`. Guards Issue 1.
   - Note: `FormattedMessage` is set on `LogRecord.ILoggerData.FormattedMessage`
     (internal field). Access via `InternalsVisibleTo` from `OpenTelemetry.Tests`
     (Session 0a Sec.4.G).

3. **`OpenTelemetryLoggerOptions_IncludeScopes_True_IncludesActiveScopeInRecord`**
   (new; same file). Tier 2. Mechanism: Mock. Registers `IncludeScopes = true`,
   pushes a scope via `ILogger.BeginScope`, asserts the scope data is present in
   the captured `LogRecord`. Guards Issue 1.

4. **`OpenTelemetryLoggerOptions_ParseStateValues_True_StateIsNull_AttributesPopulated`**
   (new; same file). Tier 2. Mechanism: Mock. Registers `ParseStateValues =
   true`, emits a log with a state dictionary, asserts `LogRecord.State == null`
   and `LogRecord.Attributes` is populated. Guards Issue 1.

5. **`OpenTelemetryLoggerOptions_ParseStateValues_False_StatePreserved`**
   (new; same file). Tier 2. Mechanism: Mock. Default (`false`); same log call;
   asserts `LogRecord.State != null`. Guards Issue 1.

Risk vs reward for 3.5: moderate effort (each test needs a processor wrapper
and a log emission); high value because these are the only scenarios that
observe `OpenTelemetryLoggerOptions` effects at the consumer - without them,
the options can change without breaking any test.

### 3.6 Invalid-input characterisation (guards Issue 1)

1. **`OpenTelemetryLoggerOptions_AddProcessor_Null_ThrowsArgumentNullException`**
   (new; `OpenTelemetryLoggingExtensionsTests.cs`). Tier 1. Mechanism:
   DirectProperty + Exception. Calls
   `new OpenTelemetryLoggerOptions().AddProcessor((BaseProcessor<LogRecord>)null!)`
   and asserts `ArgumentNullException`. Extends the partial coverage from
   `VerifyExceptionIsThrownWhenImplementationFactoryIsNull` (which tests the
   factory overload, not the direct processor overload).
   - Guards Issue 1.

2. **`OpenTelemetryLoggerOptions_SetResourceBuilder_Null_ThrowsArgumentNullException`**
   (new; same file). Tier 1. Mechanism: DirectProperty + Exception. Calls
   `new OpenTelemetryLoggerOptions().SetResourceBuilder(null!)` and asserts
   `ArgumentNullException`. Guards Issue 1.

### Prerequisites and dependencies

- Section 3.2 (IConfiguration binding) and 3.3 (priority order) tests that
  set `Configure<T>` delegates alongside `IConfiguration` need no special
  env-var isolation; they are purely DI-scoped. No isolation machinery beyond
  the existing pattern in the test class is required.
- Section 3.4 (reload no-op) tests depend on the singleton-options-manager
  pathway file
  ([`../pathways/singleton-options-manager.md`](../pathways/singleton-options-manager.md))
  establishing the shared test template; the tests in 3.4 follow that template
  for this class.
- Section 3.4.3 and 3.5 tests require a mechanism to capture `LogRecord`
  values. The `TestLogProcessor` (or `InMemoryExporter<LogRecord>`) pattern
  is already established in `OpenTelemetryLoggingExtensionsTests.cs` (the
  private `TestLogProcessor` class at line 381 of the current file). No new
  shared helper is required.
- Section 3.1.2 (snapshot) depends on the snapshot-library selection
  ([entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.5, 3.6.
- **Issue 17** - Design and implement standard `OnChange` subscriber pattern.
  Guarded by: Section 3.4 (all four tests). These tests will produce a visible
  delta when a reload path is introduced for `OpenTelemetryLoggerOptions`.

Reciprocal "Baseline tests required" lines should be added to Issues 1 and 17
in `configuration-proposed-issues.md`, citing this file. Those edits happen in
the final cross-reference sweep.
