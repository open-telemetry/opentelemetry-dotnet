# Observability and Silent Failures - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/OpenTelemetry/Internal/OpenTelemetrySdkEventSource.cs` - full event catalogue
- `src/OpenTelemetry.Extensions.Hosting/Implementation/HostingExtensionsEventSource.cs` - hosting events
- `src/Shared/Options/DelegatingOptionsFactoryServiceCollectionExtensions.cs:13-71`
  (`RegisterOptionsFactory` and `TryAddSingleton` skip)
- `src/OpenTelemetry/Metrics/MeterProviderSdk.cs:476-541` - ApplySpecificationConfigurationKeys, exemplar filter parsing
- `src/Shared/Configuration/OpenTelemetryConfigurationExtensions.cs:69-85` - `TryGetBoolValue` invalid-value path
- `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryMetricsBuilderExtensionsTests.cs`
  - `ReloadOfMetricsViaIConfigurationWithExportCleanupTest`
  - `ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest`
- `test/OpenTelemetry.Tests/EventSourceTests.cs` - EventSource ID validation
- `test/OpenTelemetry.Extensions.Hosting.Tests/EventSourceTests.cs` - `HostingExtensionsEventSource` ID validation

## Event catalogue

### OpenTelemetrySdkEventSource ("OpenTelemetry-Sdk")

Config-adjacent events emitted during provider build:

<!-- markdownlint-disable MD013 -->
| Event ID | Method | Level | Message | Condition |
| --- | --- | --- | --- | --- |
| 39 | `MeterProviderSdkEvent(string)` | Verbose | `"MeterProviderSdk event: '{0}'"` | Build milestones; exemplar filter config result (valid, invalid, programmatic override) |
| 40 | `MetricReaderEvent(string)` | Verbose | `"MetricReader event: '{0}'"` | Reader build milestones |
| 44 | `OpenTelemetryLoggerProviderEvent(string)` | Verbose | `"OpenTelemetryLoggerProvider event: '{0}'"` | Logger provider build milestones |
| 46 | `TracerProviderSdkEvent(string)` | Verbose | `"TracerProviderSdk event: '{0}'"` | Tracer disabled by `OTEL_SDK_DISABLED=true`; build milestones |
| 47 | `InvalidConfigurationValue(string, string?)` | Warning | `"Configuration key '{0}' has an invalid value: '{1}'"` | `bool.TryParse` failure in `TryGetBoolValue` (e.g. invalid `OTEL_SDK_DISABLED` value) |
| 49 | `LoggerProviderSdkEvent(string)` | Verbose | `"LoggerProviderSdk event: '{0}'"` | Logger provider disabled by `OTEL_SDK_DISABLED=true`; build milestones |
<!-- markdownlint-enable MD013 -->

**Observation:** Event 47 (`InvalidConfigurationValue`) is the only Warning-level event emitted during
configuration processing. All other config-related events are Verbose. In a production app with no
EventSource listener attached at Verbose level, all config-feedback except invalid boolean values is
invisible.

### HostingExtensionsEventSource ("OpenTelemetry-Extensions-Hosting")

<!-- markdownlint-disable MD013 -->
| Event ID | Method | Level | Condition |
| --- | --- | --- | --- |
| 1 | `TracerProviderNotRegistered()` | Warning | `TelemetryHostedService` starts but no `TracerProvider` in DI |
| 2 | `MeterProviderNotRegistered()` | Warning | `TelemetryHostedService` starts but no `MeterProvider` in DI |
| 3 | `LoggerProviderNotRegistered()` | Warning | `TelemetryHostedService` starts but no `LoggerProvider` in DI |
<!-- markdownlint-enable MD013 -->

These events fire at host startup when a provider was never registered, not during configuration
processing. They are not config-adjacent in the sense of options-binding failures.

## Silent failure catalogue

The following configuration mistakes produce no Warning-level EventSource event today. The user
sees nothing actionable in logs or event listeners.

<!-- markdownlint-disable MD013 -->
| Failure | What the user configured | What the SDK does | What the user observes |
| --- | --- | --- | --- |
| **`TryAddSingleton` skip (Risk 1.3)** | Calls `RegisterOptionsFactory<T>` twice (e.g. from two extensions that both configure the same options class) | Second registration is discarded silently; `TryAddSingleton` is a no-op when the type is already registered | Options from the first registration apply; user sees wrong values with no diagnostic |
| **Invalid exemplar filter value** | Sets `OTEL_METRICS_EXEMPLAR_FILTER=bad_value` | `TryParseExemplarFilterFromConfigurationValue` returns false; Verbose Event 39 emitted; filter not applied | Filter silently stays as default or null; Verbose event invisible without a listener |
| **Invalid `OTEL_SDK_DISABLED` value** | Sets `OTEL_SDK_DISABLED=yes` | `bool.TryParse` fails; **Warning** Event 47 emitted; SDK stays enabled | Event 47 visible at Warning level - this IS observable, unlike the others above |
| **`PostConfigure` registered after DI build** | Calls `services.PostConfigure<T>()` after `IServiceProvider` is already built | No-op; `PostConfigure` callbacks registered post-build are never invoked | Options values are wrong; no event emitted |
| **Options class constructor receives wrong `IConfiguration`** | Non-host DI path without `AddEnvironmentVariables()` | Constructor reads from an empty or incomplete `IConfiguration`; env-var defaults do not apply | Options silently use code defaults rather than env var values |
<!-- markdownlint-enable MD013 -->

**Adjacent finding (non-config):** `MeterProviderSdk.cs:192-197` contains a `// todo: Log` comment in
`MeasurementsCompleted` when `state` is not a `MetricState`. This is a silent runtime failure outside
the config domain. Recorded in the adjacent findings register (entry doc Sec.9); no test recommendation
in this file.

## 1. Existing coverage

<!-- markdownlint-disable MD013 -->
| File:method | Scenario summary | Observation mechanism | Env-var isolation status |
| --- | --- | --- | --- |
| `OpenTelemetryMetricsBuilderExtensionsTests.ReloadOfMetricsViaIConfigurationWithExportCleanupTest` | `IOptionsMonitor` reload + `InMemoryEventListener` asserts on Event 39 (`MeterProviderSdkEvent`) | EventSource (`InMemoryEventListener`) | No env var |
| `OpenTelemetryMetricsBuilderExtensionsTests.ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest` | Same; no export cleanup variant | EventSource (`InMemoryEventListener`) | No env var |
| `OpenTelemetryServicesExtensionsTests.AddOpenTelemetry_StartWithExceptionsThrows` | Exceptions in deferred `Configure` callbacks propagate; not silently swallowed | Behavioural (exception thrown) | None |
| `EventSourceTests.EventSourceTests_HostingExtensionsEventSource` | Validates `HostingExtensionsEventSource` event IDs | Direct property (event ID integers) | None |
| `OpenTelemetry.Tests/EventSourceTests.*` | Validates `OpenTelemetrySdkEventSource` event IDs | Direct property | None |
<!-- markdownlint-enable MD013 -->

The two reload tests are the only existing tests that make config-adjacent EventSource assertions.
The EventSource ID tests validate wiring, not emitted-event behaviour.

## 2. Scenario checklist and gap analysis

### 2.1 Invalid configuration value observability

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_SDK_DISABLED=yes` (invalid bool) -> Event 47 emitted | None | Event 47 at Warning level emitted; SDK stays enabled | Missing |
| `OTEL_METRICS_EXEMPLAR_FILTER=bad` (invalid string) -> Event 39 emitted | None | Verbose Event 39 emitted; filter not applied | Missing |
| `OTEL_BSP_SCHEDULE_DELAY=not_a_number` -> Event 47 or similar | None | Falls back to default; no Warning event (verify in Session 1+) | Missing |
<!-- markdownlint-enable MD013 -->

### 2.2 Silent failure baselines

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `RegisterOptionsFactory<T>` called twice -> second registration silently discarded | None | `TryAddSingleton` no-ops; no event | Missing |
| `PostConfigure` registered after DI build -> never invoked, no event | None | Silent no-op; no event | Missing |
| Non-host DI without `AddEnvironmentVariables()` -> env vars not read, no event | None | Silent; wrong options values applied | Missing |
<!-- markdownlint-enable MD013 -->

### 2.3 EventSource reload assertions (existing - for reference)

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor` reload emits `MeterProviderSdkEvent` (Event 39) | `ReloadOfMetricsViaIConfigurationWithExportCleanupTest` | Event 39 asserted via `InMemoryEventListener` | Covered |
| `IOptionsMonitor` reload emits `MeterProviderSdkEvent` without export cleanup | `ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest` | Event 39 asserted | Covered |
<!-- markdownlint-enable MD013 -->

## 3. Recommendations

### 3.1 `SdkDisabled_InvalidBoolValue_Event47Emitted`

**Target test name:** `SdkDisabled_InvalidBoolValue_Event47Emitted`
**Location:** `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderBaseTests.cs`
**Tier:** 1
**Observation mechanism:** `EventSource` - attach `InMemoryEventListener` (already available via linked
source in this test project) and assert that Event 47 is emitted with key `"OTEL_SDK_DISABLED"` and
the supplied invalid value string. The SDK infrastructure in `OpenTelemetry.Tests/Shared/` already
contains `InMemoryEventListener` and `EventSourceTestHelper`.
**Guards issues:** Issues 6 (diagnostic logging) and 17 (OnChange EventSource events) - this test pins
the pre-6 baseline so that the Issue 6 work produces a visible test delta.

```csharp
// BASELINE: today an invalid OTEL_SDK_DISABLED value silently leaves the SDK enabled
// and emits only Event 47 (Warning). No higher-level diagnostic is produced.
// Expected to change under Issue #6 (diagnostic logging for configuration failures).
// Guards risks: Risk 4.7 (insufficient diagnostic observability).
// Observation: EventSource (InMemoryEventListener) - brittleness: Event 47 message format change breaks this.
// Coverage index: pathway.observability-and-silent-failures.sdk-disabled.invalid-value-event47
```

**Risk vs reward:** Low effort (InMemoryEventListener is already available). High value: this is the
only Warning-level event emitted during configuration processing today; pinning it before Issue 6
lands gives a clear before/after delta.

### 3.2 `ExemplarFilter_InvalidValue_VerboseEvent39Emitted_NoWarning`

**Target test name:** `ExemplarFilter_InvalidValue_VerboseEvent39Emitted_NoWarning`
**Location:** `test/OpenTelemetry.Tests/Metrics/MetricExemplarTests.cs`
**Tier:** 2
**Observation mechanism:** `EventSource` - attach `InMemoryEventListener` at Verbose level; assert
that Event 39 (`MeterProviderSdkEvent`) is emitted with a message fragment containing the invalid
value, and that Event 47 (`InvalidConfigurationValue`) is **not** emitted (the exemplar filter path
does not use `TryGetBoolValue` and emits no Warning).
**Guards issues:** Issues 6 and 17.

```csharp
// BASELINE: today an invalid OTEL_METRICS_EXEMPLAR_FILTER value emits only a Verbose Event 39.
// No Warning-level event is emitted; the failure is silent to any listener not at Verbose level.
// Expected to change under Issue #6 (promote to Warning or add a dedicated diagnostic event).
// Guards risks: Risk 4.7.
// Observation: EventSource (InMemoryEventListener at Verbose) - brittleness: message-text matching
// is fragile; prefer asserting event ID and key presence rather than full message content.
// Coverage index: pathway.observability-and-silent-failures.exemplar-filter.invalid-value-verbose-only
```

**Risk vs reward:** Medium effort (Verbose listener setup). High value: documents that the exemplar
filter invalid-value path is observably weaker than the boolean-value path (Verbose only vs Warning),
giving Issue 6 a concrete target.

### 3.3 `RegisterOptionsFactory_CalledTwice_SecondRegistrationSilentlyDiscarded`

**Target test name:** `RegisterOptionsFactory_CalledTwice_SecondRegistrationSilentlyDiscarded`
**Location:** `test/OpenTelemetry.Tests/` - a new test file for `DelegatingOptionsFactory` shared
infrastructure, or the existing options test for a class known to call `RegisterOptionsFactory`
(e.g. `OtlpExporterOptionsTests` or `SdkLimitOptionsTests`).
**Tier:** 2
**What to test:** Call `RegisterOptionsFactory<T>` twice on the same `IServiceCollection` with
different factory delegates. Build the provider and resolve the options. Assert that the value from
the **first** registration applies. Assert that no EventSource event is emitted (there is no event
today - the test pins silence as today's baseline).
**Observation mechanism:** `DI` (`IOptionsMonitor<T>`) + `EventSource` (assert event count is zero
for events 6, 8, 47, 39 during the operation).
**Guards issues:** Issues 6 and 17.

```csharp
// BASELINE: today the second RegisterOptionsFactory call is silently discarded via TryAddSingleton.
// No EventSource event is emitted. The user has no diagnostic.
// Expected to change under Issue #6 (emit a Warning or Informational event when second registration
// is skipped, or under Issue #17 if the skip is surfaced as a configuration-change event).
// Guards risks: Risk 1.3 (TryAddSingleton first-writer-wins).
// Observation: DI + EventSource - assert options value from first factory AND zero diagnostic events.
// Coverage index: pathway.observability-and-silent-failures.register-options-factory.silent-skip
```

**Risk vs reward:** Medium effort. Very high value: Risk 1.3 is identified as a likely source of
hard-to-debug misconfiguration; this test creates the first regression protection for that scenario
and serves as the before-state for Issue 6.

### 3.4 `OpenTelemetrySdkEventSource_EventIds_StableAfterConfigRefactor`

**Target test name:** `OpenTelemetrySdkEventSource_EventIds_StableAfterConfigRefactor`
**Location:** `test/OpenTelemetry.Tests/EventSourceTests.cs` (extend the existing EventSource ID
validation test or add a focused assertion).
**Tier:** 1
**What to test:** Assert that Event IDs 39, 46, 47, 49 (the config-adjacent events catalogued in the
event catalogue above) remain bound to their current method names and levels. This guards against
accidental renumbering during a refactor.
**Observation mechanism:** Direct property (event ID constants via `EventAttribute`).
**Guards issues:** Issues 6 and 17.

```csharp
// BASELINE: pins current event ID assignments for config-adjacent events.
// Expected to change under Issue #17 (new EventSource events added for OnChange lifecycle).
// Observation: DirectProperty (EventAttribute.EventId) - low brittleness.
// Coverage index: pathway.observability-and-silent-failures.event-source.config-event-ids-stable
```

## Guards issues

- Issue 6 (diagnostic logging for `RegisterOptionsFactory` silent skip): **direct guard** -
  Recommendation 3.3 pins the before-state (zero events emitted); once Issue 6 lands the test
  asserts the new event is emitted.
- Issue 17 (OnChange subscriber pattern / EventSource events for config-change lifecycle): **direct
  guard** - Recommendations 3.1 and 3.2 pin the before-state for invalid-config-value observability;
  once Issue 17 lands the tests assert the new events are emitted.
