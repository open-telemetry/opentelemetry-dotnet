# ExperimentalOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:8`.
- Public parameterless constructor (builds its own env-var-backed
  `IConfiguration`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:18-21`.
- Internal constructor that takes `IConfiguration` (DI path and test path) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:23-54`.
- Property declarations (all `get`-only; no setters):
  - `EmitLogEventAttributes` (bool, default `false`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:59`.
  - `EnableInMemoryRetry` (bool, default `false`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:68`.
  - `EnableDiskRetry` (bool, default `false`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:73`.
  - `DiskRetryDirectoryPath` (string?, default `null`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:78`.
- Env-var name constants (all on the class itself):
  - `EmitLogEventEnvVar` = `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:12`.
  - `OtlpRetryEnvVar` = `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:14`.
  - `OtlpDiskRetryDirectoryPathEnvVar` = `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:16`.
- Semantic constant (not an env var):
  - `LogRecordEventIdAttribute` = `"logrecord.event.id"` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:10`.
- DI factory registration (via `RegisterOptionsFactory`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:55`
  (`services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration))`).
  This registration is made by `AddOtlpExporterSharedServices`, which is
  called from `AddOtlpExporterTracingServices` and the signal-specific
  equivalents.

### Direct consumer sites

Consumers that read `ExperimentalOptions` properties (pins which
behaviours are only observable at the consumer):

- `OtlpExporterOptionsExtensions.GetExportTransmissionHandler` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptionsExtensions.cs:71-98`
  reads `EnableInMemoryRetry`, `EnableDiskRetry`, and `DiskRetryDirectoryPath`
  to select the concrete `OtlpExporterTransmissionHandler` subtype and its
  storage path. This is the primary gate for retry configuration.
- `ProtobufOtlpLogSerializer` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpLogSerializer.cs:199`
  reads `EmitLogEventAttributes` to decide whether to append event-id
  attributes to each log record during serialisation.
- `OtlpTraceExporter` constructor -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporter.cs:45-49`
  receives an `ExperimentalOptions` instance and passes it to
  `GetExportTransmissionHandler`.
- `OtlpLogExporter` constructor -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporter.cs:46-52`
  stores the instance; both `GetExportTransmissionHandler` and the serialiser
  path read from it.
- `OtlpMetricExporter` constructor -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporter.cs:43-46`
  receives the instance and passes it to `GetExportTransmissionHandler`.
- `OtlpTraceExporterHelperExtensions` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:96`
  resolves `IOptionsMonitor<ExperimentalOptions>.Get(finalOptionsName)` from
  the service provider (AddOtlpExporter pathway).
- `OtlpLogExporterHelperExtensions` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporterHelperExtensions.cs:73`
  and `:121` resolve `ExperimentalOptions` via
  `GetOptions(..., (sp, c, n) => new ExperimentalOptions(c))` (AddOtlpExporter
  pathway for logging).
- `OtlpMetricExporterExtensions` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMetricExporterExtensions.cs:89`
  and `:153` resolve `IOptionsMonitor<ExperimentalOptions>.Get(finalOptionsName)`.
- `OtlpExporterBuilder` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:182`
  resolves `IOptionsMonitor<ExperimentalOptions>.Get(name)` for the
  `UseOtlpExporter` pathway; passes the instance to log, metrics, and tracing
  exporters at lines `:203`, `:221`, `:236`.
- `OtlpExporterBuilderOptions` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:16`
  holds the instance as a `readonly` field; constructor at `:29` takes it as
  a parameter.

**Immutability note.** All four properties are `get`-only (no setter).
The constructor reads env vars once via the injected `IConfiguration` and
stores the results into the backing fields. There is no mechanism to update
property values after construction. Any env-var change that occurs after
the first `IOptionsMonitor<ExperimentalOptions>.Get(name)` call is not
visible to that resolved instance or to any component constructed from it.
A process restart is the only way to pick up a changed value. This
restart-required contract is the baseline this file pins.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

`File:method` is abbreviated to the test-method name where the file is
unambiguous. Projects:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

### 1.1 `OtlpExporterOptionsExtensionsTests.cs` (OTPT)

One test method exercises `ExperimentalOptions`. It is a Theory that
drives eight `[InlineData]` rows.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterOptionsExtensionsTests.GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` | Constructs `ExperimentalOptions(configuration)` directly with an in-memory `IConfiguration` that includes `OtlpRetryEnvVar`; calls `GetExportTransmissionHandler`; asserts handler subtype (`OtlpExporterTransmissionHandler`, `OtlpExporterRetryTransmissionHandler`, `OtlpExporterPersistentStorageTransmissionHandler`), export-client type, and timeout value | Reflection on internal `TransmissionHandler.ExportClient` and `TimeoutMilliseconds` fields (via `AssertTransmissionHandler` helper) | Not env-var dependent (uses in-memory `IConfiguration` directly; no process-global env var set) |

**Coverage summary for Section 1.** One test method with eight Theory
rows. It exercises `EnableInMemoryRetry` and `EnableDiskRetry` (via
`OtlpRetryEnvVar`) across both `Grpc` and `HttpProtobuf` protocols. No
test exercises `EmitLogEventAttributes` (`EmitLogEventEnvVar`), the
`DiskRetryDirectoryPath` path value, the unknown-retry-policy throw path,
or the default-state (all flags `false`/`null`) in isolation.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or a dash for none.

### 2.1 Constructor env-var reads (per property)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `EmitLogEventAttributes` set from `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES=true` | - | `TryGetBoolValue` parses the env var; `EmitLogEventAttributes = true` | missing |
| `EmitLogEventAttributes` default (env var absent or `false`) | - | `EmitLogEventAttributes = false`; no assignment made in ctor | missing |
| `EnableInMemoryRetry` set from `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY=in_memory` | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` (Theory rows `"in_memory"`) | `EnableInMemoryRetry = true`; handler type is `OtlpExporterRetryTransmissionHandler` | covered (at consumer level via handler type assertion) |
| `EnableDiskRetry` set from `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY=disk` | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` (Theory rows `"disk"`) | `EnableDiskRetry = true`; handler type is `OtlpExporterPersistentStorageTransmissionHandler` | covered (at consumer level via handler type assertion) |
| `DiskRetryDirectoryPath` set from `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` when `retry=disk` | - | String stored as-is; used in `Path.Combine` to set handler directory | missing |
| `DiskRetryDirectoryPath` defaults to `Path.GetTempPath()` when `retry=disk` but directory env var absent | - | Fallback at line 46 of source; path is `Path.GetTempPath()` | missing |
| All flags at their defaults (no env vars set) | - | `EmitLogEventAttributes = false`, `EnableInMemoryRetry = false`, `EnableDiskRetry = false`, `DiskRetryDirectoryPath = null` | missing |

### 2.2 Priority order

`ExperimentalOptions` does not participate in the `Configure<T>` /
`PostConfigure<T>` pipeline. The constructor reads from the injected
`IConfiguration` directly, and the DI factory is a plain lambda with no
post-configure step. There is no programmatic setter path because all
properties are `get`-only. Therefore the only meaningful priority question
is: does an in-memory `IConfiguration` entry override a process env var?
Because `IConfiguration` is passed in explicitly, the caller controls the
source; the constructor does not build its own `ConfigurationBuilder` in
the DI-registered factory path (the lambda receives the DI `IConfiguration`
directly), but the public parameterless constructor does build its own
`ConfigurationBuilder().AddEnvironmentVariables()`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfiguration` (in-memory) takes the value as-is (no env-var layer in DI path) | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` (partial: only retry env var exercised) | In DI path, `IConfiguration` from DI root is the sole source; no separate env-var layer added in the factory lambda | partial (one env var exercised; `EmitLogEventEnvVar` not exercised) |
| Public parameterless constructor reads process env vars | - | `new ExperimentalOptions()` builds its own `ConfigurationBuilder().AddEnvironmentVariables().Build()` and passes it to the `IConfiguration` constructor; changes to env vars before construction are reflected | missing |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All properties at their defaults when no env vars are set and no configuration is provided | - | `EmitLogEventAttributes = false`, `EnableInMemoryRetry = false`, `EnableDiskRetry = false`, `DiskRetryDirectoryPath = null` | missing |

### 2.4 Named options

N/A - single instance, immutable intent.

`ExperimentalOptions` is registered via `RegisterOptionsFactory` as a
singleton-scoped factory (one instance per named options key). In the
`AddOtlpExporter` pathway, a single `IOptionsMonitor<ExperimentalOptions>`
resolved at `Options.DefaultName` is used for all signals. In the
`UseOtlpExporter` pathway, the builder resolves it with the builder `name`
argument. There are no user-facing "named" `ExperimentalOptions` variants,
and there is no signal-specific env-var namespace (the env-var names are
identical regardless of which signal's exporter resolves the instance). No
named-options test scenarios apply beyond confirming the single shared
instance carries the correct values.

### 2.5 Invalid-input characterisation

| Property / input | Malformed input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` | Unknown string (not `"in_memory"` or `"disk"`) | `throw new NotSupportedException(...)` - source line 51 | - | missing |
| `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` | Non-bool string (e.g. `"yes"`) | `TryGetBoolValue` returns `false`; flag stays `false`; no throw | - | missing |
| `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` | Empty string when `retry=disk` | `TryGetStringValue` returns `false` on empty? (depends on implementation); if absent, fallback to temp | - | missing |

All rows marked **missing** may be affected by Issue 1 (validation). For
`ExperimentalOptions` the `NotSupportedException` throw on unknown retry
is already an eager-validation pattern; Issue 1 would align other input
failures to the same model. Tests pinning today's behaviour give Issue 1
a visible delta.

### 2.6 Reload no-op baseline

`ExperimentalOptions` is explicitly restart-required. This is the critical
subsection for this class.

All four properties are `get`-only. The constructor runs once per instance.
`IOptionsMonitor<ExperimentalOptions>.OnChange` can fire (if the backing
`IConfiguration` is reloaded), but the already-constructed
`OtlpExporterTransmissionHandler` instance and the already-stored
`OtlpLogExporter.experimentalOptions` field cannot observe the changed
values without rebuilding the exporter. No existing code subscribes to
`OnChange` for this class.

The expected baseline: `IConfigurationRoot.Reload()` fires `OnChange`
callbacks on `IOptionsMonitor<ExperimentalOptions>`, but the built
exporter continues to use the handler type and log-serialiser flag that
were in effect at construction time.

This baseline is the test that would flip under any future
"ExperimentalOptions immutability" workstream that makes the class mutable
and wires it to `IOptionsMonitor`. Until that workstream lands, the test
documents and asserts the restart-required contract.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<ExperimentalOptions>.OnChange` fires on `IConfigurationRoot.Reload()` | - | Callbacks registered via `OnChange` fire; no built component acts on them | missing |
| Reload of `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` -> built `OtlpExporterTransmissionHandler` type unchanged | - | Handler subtype fixed at exporter construction; reload does not replace the handler | missing |
| Reload of `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` -> built `OtlpLogExporter.experimentalOptions.EmitLogEventAttributes` unchanged | - | Value fixed at instance construction; no re-read mechanism exists | missing |

All three rows are expected to flip (from "no-op" to "reacts") when an
immutability workstream for `ExperimentalOptions` lands. The test that
asserts the current no-op is the diagnostic signal that the workstream
broke the expected baseline.

### 2.7 Consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `EnableInMemoryRetry = true` -> `GetExportTransmissionHandler` returns `OtlpExporterRetryTransmissionHandler` | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` | Correct subtype returned | covered |
| `EnableDiskRetry = true` -> `GetExportTransmissionHandler` returns `OtlpExporterPersistentStorageTransmissionHandler` | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` | Correct subtype returned | covered |
| `EnableDiskRetry = true` + `DiskRetryDirectoryPath` set -> handler uses custom directory in `Path.Combine` | - | Handler receives `Path.Combine(DiskRetryDirectoryPath, "traces"|"metrics"|"logs")` | missing |
| `EnableDiskRetry = true` + no directory env var -> handler uses `Path.GetTempPath()` | - | Fallback at source line 46; handler directory is `Path.Combine(Path.GetTempPath(), "traces"|...)` | missing |
| No flags set -> `GetExportTransmissionHandler` returns base `OtlpExporterTransmissionHandler` | `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` (Theory rows `retryStrategy=null`) | Base handler returned | covered |
| `EmitLogEventAttributes = true` -> log records include event-id attributes in serialised output | - | `ProtobufOtlpLogSerializer` appends `logrecord.event.id` attribute block when `EmitLogEventAttributes = true` | missing |
| `EmitLogEventAttributes = false` -> log records omit event-id attributes | - | Block skipped at serialiser line 199 | missing |

---

## 3. Recommendations

One entry per gap. Each recommendation targets a reviewable PR unit. Test
name follows the dominant `Subject_Condition_Expected` convention from the
Session 0a naming survey. Target location is the existing test file for
the scenario. Tier mapping per entry-doc Section 3. Observation-mechanism
labels match Section 2 of the entry doc.

### 3.1 Default-state baseline

1. **`ExperimentalOptions_Defaults_AllFlagsFalse`** (new test in
   `OtlpExporterOptionsExtensionsTests.cs`, alongside the existing Theory).
   - Tier 1. Mechanism: DirectProperty. Constructs
     `new ExperimentalOptions(new ConfigurationBuilder().Build())` (empty
     configuration; no env vars injected) and asserts
     `EmitLogEventAttributes == false`, `EnableInMemoryRetry == false`,
     `EnableDiskRetry == false`, `DiskRetryDirectoryPath == null`.
   - Guards Issue 1. Risks pinned: none beyond the defaults drift risk.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour. No planned change.
     // Observation: DirectProperty - all get-only; no DI required.
     // Coverage index: experimental-options.defaults.all-properties
     ```

   - Risk vs reward: trivial to author; high value as the anchor for all
     other scenarios in this file.

2. **`ExperimentalOptions_Default_Snapshot`** (new test in
   `OtlpExporterOptionsExtensionsTests.cs` or a dedicated `Snapshots/`
   subfolder per the snapshot-library selection in entry-doc Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD by maintainers). This class
     is the candidate "pilot" class recommended in Appendix A because it
     is internal, has four properties, and has no named-options complexity.
   - Guards Issue 1.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins whole-options default shape.
     // Snapshot update expected on any additive experimental flag.
     // Coverage index: experimental-options.defaults.snapshot
     ```

   - Risk vs reward: low per-test cost; validates the snapshot workflow
     before applying it to larger classes. Depends on the snapshot-library
     decision (entry-doc Appendix A).

### 3.2 Env-var reads per property (missing flags)

1. **`ExperimentalOptions_EmitLogEventAttributes_SetFromEnvVar`** (new;
   `OtlpExporterOptionsExtensionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs
     `new ExperimentalOptions(configuration)` where `configuration` is an
     in-memory `IConfiguration` with `EmitLogEventEnvVar = "true"`.
     Asserts `EmitLogEventAttributes == true`.
   - Guards Issue 1.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour.
     // Expected to change under Issue 1 (IValidateOptions<T>).
     // Guards risks: none beyond env-var read correctness.
     // Observation: DirectProperty - get-only bool; no DI required.
     // Coverage index: experimental-options.emit-log-event-attributes.env-var
     ```

   - Risk vs reward: low effort; closes the only env-var gap in the
     current Theory.

2. **`ExperimentalOptions_EmitLogEventAttributes_Default_WhenEnvVarAbsent`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Companion to the
   above; asserts `false` when the key is not in the configuration.
   Coverage index: `experimental-options.emit-log-event-attributes.default`.

3. **`ExperimentalOptions_DiskRetry_DirectoryPath_UsesEnvVar`** (new;
   same file).
   - Tier 1. Mechanism: DirectProperty. Constructs with
     `OtlpRetryEnvVar = "disk"` and
     `OtlpDiskRetryDirectoryPathEnvVar = "/custom/path"`. Asserts
     `DiskRetryDirectoryPath == "/custom/path"`.
   - Guards Issue 1. Coverage index:
     `experimental-options.disk-retry-directory-path.env-var`.

4. **`ExperimentalOptions_DiskRetry_DirectoryPath_DefaultsToTempPath`**
   (new; same file).
   - Tier 1. Mechanism: DirectProperty. Constructs with
     `OtlpRetryEnvVar = "disk"` only (no directory env var). Asserts
     `DiskRetryDirectoryPath == Path.GetTempPath()`.
   - Guards Issue 1. Coverage index:
     `experimental-options.disk-retry-directory-path.default`.

### 3.3 Invalid-input characterisation (guards Issue 1)

1. **`ExperimentalOptions_UnknownRetryPolicy_ThrowsNotSupportedException`**
   (new; `OtlpExporterOptionsExtensionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty (exception). Constructs
     `new ExperimentalOptions(configuration)` with
     `OtlpRetryEnvVar = "unknown_value"`. Asserts
     `NotSupportedException` is thrown.
   - Guards Issue 1. Coverage index:
     `experimental-options.retry-policy.invalid-input`.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour.
     // Expected to change under Issue 1 (IValidateOptions<T>);
     // today this throws at construction time rather than at validate-on-start.
     // Observation: DirectProperty (exception).
     // Coverage index: experimental-options.retry-policy.invalid-input
     ```

   - Risk vs reward: trivial to author; pins a distinct behaviour
     (construction-time throw vs `IValidateOptions` fail-on-start) so
     Issue 1 has a clear migration target.

2. **`ExperimentalOptions_EmitLogEventAttributes_InvalidBool_DefaultsToFalse`**
   (new; same file).
   - Tier 1. Mechanism: DirectProperty. Constructs with
     `EmitLogEventEnvVar = "yes"` (not a recognised bool string for
     `TryGetBoolValue`). Asserts `EmitLogEventAttributes == false` (silent
     ignore today).
   - Guards Issue 1. Coverage index:
     `experimental-options.emit-log-event-attributes.invalid-input`.
   - Code-comment hint: "BASELINE: pins silent-ignore. Expected to change
     under Issue 1 (validation)."

### 3.4 Consumer-observed effects currently missing

1. **`ExperimentalOptions_DiskRetry_CustomDirectory_FlowsToHandler`** (new;
   `OtlpExporterOptionsExtensionsTests.cs`).
   - Tier 1. Mechanism: Reflection on
     `OtlpExporterPersistentStorageTransmissionHandler`'s storage-directory
     field (the existing `AssertTransmissionHandler` helper already inspects
     handler type and timeout; extend it or add a peer assertion for the
     directory). If an internal accessor exists or can be added (non-goals
     section: seams require a production change; flag for maintainer
     decision), prefer InternalAccessor over Reflection.
   - Guards Issue 1. Coverage index:
     `experimental-options.disk-retry-directory-path.consumer-effect`.
   - Risk vs reward: moderate brittleness (reflection on persistent-storage
     handler) but pins a non-obvious path (custom directory vs temp
     fallback) that is otherwise invisible at the options level.

2. **`ExperimentalOptions_EmitLogEventAttributes_AppearsInLogRecordOutput`**
   (new; `OtlpLogExporterTests.cs` or a dedicated serialiser test file).
   - Tier 2. Mechanism: Mock (use a `DelegatingExporter<LogRecord>` to
     capture export calls; wire `OtlpLogExporter` with
     `EmitLogEventAttributes = true` and a log record with a non-zero
     `EventId`; serialise and assert the presence of the
     `logrecord.event.id` attribute in the protobuf payload).
   - Guards Issue 1. Risks pinned: none.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour. No planned change.
     // Observation: Mock - behavioural; does not reflect private fields.
     // Coverage index: experimental-options.emit-log-event-attributes.consumer-effect
     ```

   - Risk vs reward: moderate effort (requires constructing a real log
     record and inspecting serialised output); high value because
     `EmitLogEventAttributes` is otherwise entirely untested end-to-end.

### 3.5 Reload no-op baseline (critical section)

This is the test that would flip when an "ExperimentalOptions
immutability" workstream lands. The test asserts the current no-op, giving
the workstream a concrete delta.

1. **`ExperimentalOptions_ReloadOfConfiguration_DoesNotChangeBuiltHandlerType`**
   (new; `OtlpExporterOptionsExtensionsTests.cs` or a standalone reload
   test class).
   - Tier 2. Mechanism: DI + Behavioural (type-check the resolved handler
     after `IConfigurationRoot.Reload()`). Build an `IServiceCollection`
     with `AddOtlpExporterTracingServices`, start with `retry=null`,
     build the provider, resolve `IOptionsMonitor<ExperimentalOptions>`,
     subscribe to `OnChange`, swap the in-memory configuration source to
     `retry=in_memory`, trigger `IConfigurationRoot.Reload()`. Assert:
     (a) `OnChange` fires (monitors do propagate) and (b) the
     `OtlpExporterTransmissionHandler` instance obtained before the reload
     is still a base `OtlpExporterTransmissionHandler`, not
     `OtlpExporterRetryTransmissionHandler`.
   - Guards Issue 1 and any future immutability issue.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins restart-required contract.
     // Expected to flip when ExperimentalOptions immutability workstream lands
     // and ExperimentalOptions is wired to IOptionsMonitor with live reload.
     // Observation: DI + Mock - no reflection; handler type is public.
     // Coverage index: experimental-options.retry-policy.reload-no-op
     ```

   - Risk vs reward: moderate effort (requires DI setup and reload
     trigger); high value because this is the only test that would detect
     a silent introduction of live-reload for experimental flags without a
     deliberate choice to do so.

2. **`ExperimentalOptions_ReloadOfConfiguration_DoesNotChangeEmitLogEventAttributes`**
   (new; same location).
   - Tier 2. Mechanism: DI + InternalAccessor (read
     `OtlpLogExporter.experimentalOptions.EmitLogEventAttributes` if
     accessible; otherwise use Behavioural via mock log record export).
     Build, flip `EmitLogEventEnvVar` in the configuration, reload, assert
     the already-built `OtlpLogExporter` still uses the pre-reload flag.
   - Guards same issues as above.
   - Coverage index:
     `experimental-options.emit-log-event-attributes.reload-no-op`.

### Prerequisites and dependencies

- 3.1 and 3.2 have no env-var isolation concern: all recommended tests use
  in-memory `IConfiguration`; no process-global env var is mutated. The
  existing `[Collection]` attribute on `OtlpExporterOptionsExtensionsTests`
  applies by default but these tests would be safe under parallelism.
- 3.5 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  to establish the shared pattern (build, reload, assert). Produce that
  file before implementing 3.5 tests.
- 3.1.2 (snapshot) depends on the snapshot-library selection
  ([entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.4.
- Any future issue covering an "ExperimentalOptions immutability"
  workstream (not yet numbered). Guarded by: Section 3.5.

Reciprocal "Baseline tests required" lines should be added to Issue 1 in
[`configuration-proposed-issues.md`](../../configuration-proposed-issues.md)
citing this file. That edit happens in the final cross-reference sweep.
