# SdkLimitOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration (internal sealed class; `DefaultSdkLimit = 128`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:8-10`.
- Backing private fields for the cascade-aware properties -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:11-22`.
- Public parameterless constructor (builds its own env-var-backed
  `IConfiguration`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:24-27`.
- Internal constructor that accepts `IConfiguration` - reads all ten env
  vars at construction time -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:29-46`.
- `SetIntConfigValue` private static helper (applies setter or default if
  key absent) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:164-174`.
- Property declarations:
  - `AttributeValueLengthLimit` (auto; `null` default) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:51`.
  - `AttributeCountLimit` (auto; default 128) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:56`.
  - `SpanAttributeValueLengthLimit` (cascade getter falls back to
    `AttributeValueLengthLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:64-72`.
  - `SpanAttributeCountLimit` (cascade getter falls back to
    `AttributeCountLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:80-88`.
  - `SpanEventCountLimit` (auto; default 128) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:93`.
  - `SpanLinkCountLimit` (auto; default 128) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:98`.
  - `SpanEventAttributeCountLimit` (cascade getter falls back to
    `SpanAttributeCountLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:106-114`.
  - `SpanLinkAttributeCountLimit` (cascade getter falls back to
    `SpanAttributeCountLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:122-130`.
  - `LogRecordAttributeValueLengthLimit` (cascade getter falls back to
    `AttributeValueLengthLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:138-146`.
  - `LogRecordAttributeCountLimit` (cascade getter falls back to
    `AttributeCountLimit`) -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:154-162`.

### Cascade chain (current constructor-time implementation)

The cascade is implemented entirely via property getters backed by
`*Set` boolean fields. Setting a property sets its `*Set` flag to
`true`; the getter returns the backing field only when the flag is set,
otherwise it falls back to the parent property. This makes the cascade a
read-time decision, not a write-time one. Issue 5 proposes moving the
cascade logic to `PostConfigure<T>` so it operates within the DI
options pipeline instead.

Current cascade rules (read-time, property getter level):

```
SpanAttributeValueLengthLimit -> AttributeValueLengthLimit
SpanAttributeCountLimit -> AttributeCountLimit
SpanEventAttributeCountLimit -> SpanAttributeCountLimit -> AttributeCountLimit
SpanLinkAttributeCountLimit -> SpanAttributeCountLimit -> AttributeCountLimit
LogRecordAttributeValueLengthLimit -> AttributeValueLengthLimit
LogRecordAttributeCountLimit -> AttributeCountLimit
```

### DI registration

`SdkLimitOptions` is registered via `RegisterOptionsFactory` only for
tracing and logging signal registrations, not for metrics:

- Tracing (`AddOtlpExporterTracingServices`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:48`.
- Logging (`AddOtlpExporterLoggingServices`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:14`.
- Factory registration -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:59`.

The factory always passes the ambient `IConfiguration`; there is no
named-options variant. The code comments in both
`OtlpTraceExporterHelperExtensions.cs:86-90` and
`OtlpExporterBuilder.cs:178-181` explicitly state that `SdkLimitOptions`
is treated as a single default instance (no named-options dispatch).

### Consumer sites (serializers read the limit values)

The OTLP serializers receive a `SdkLimitOptions` instance by value at
export time. Each method reads specific properties:

- `ProtobufOtlpTraceSerializer.WriteActivityTags` reads
  `SpanAttributeCountLimit` and `AttributeValueLengthLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpTraceSerializer.cs:288-289`.
- `ProtobufOtlpTraceSerializer.WriteEventAttributes` reads
  `SpanEventAttributeCountLimit` and `AttributeValueLengthLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpTraceSerializer.cs:387-388`.
- `ProtobufOtlpTraceSerializer.WriteSpanEvents` reads
  `SpanEventCountLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpTraceSerializer.cs:352`.
- `ProtobufOtlpTraceSerializer.WriteSpanLinks` reads
  `SpanLinkCountLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpTraceSerializer.cs:426`.
- `ProtobufOtlpTraceSerializer.WriteLinkAttributes` reads
  `SpanLinkAttributeCountLimit` and `AttributeValueLengthLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpTraceSerializer.cs:470-471`.
- `ProtobufOtlpLogSerializer.WriteLogRecord` reads
  `LogRecordAttributeValueLengthLimit` and `LogRecordAttributeCountLimit` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/Serializer/ProtobufOtlpLogSerializer.cs:166-167`.
- `OtlpTraceExporter` stores `SdkLimitOptions` at construction and
  passes it into each `WriteTraceData` call -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporter.cs:20,51`.
- `OtlpLogExporter` stores `SdkLimitOptions` at construction and
  passes it into each `WriteLogsData` call -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpLogExporter.cs:20,51`.
- `OtlpTraceExporterHelperExtensions` resolves `IOptionsMonitor<SdkLimitOptions>.CurrentValue`
  at processor-build time -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:90`.

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md) (Section 1.B, `SdkLimitOptionsTests.cs`
rows). Inventory only.

`OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

### 1.1 `SdkLimitOptionsTests.cs` (OTPT)

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `SdkLimitOptionsTests.SdkLimitOptionsDefaults` | All properties at their defaults with no env vars or config | DirectProperty | Class-level `IDisposable` snapshot/restore (constructor clears env vars) |
| `SdkLimitOptionsTests.SdkLimitOptionsIsInitializedFromEnvironment` | All env vars read correctly (`OTEL_ATTRIBUTE_*`, `OTEL_SPAN_*`, `OTEL_EVENT_*`, `OTEL_LINK_*`, `OTEL_LOGRECORD_*`) | DirectProperty | Class-level `IDisposable` snapshot/restore |
| `SdkLimitOptionsTests.SpanAttributeValueLengthLimitFallback` | `AttributeValueLengthLimit` -> `SpanAttributeValueLengthLimit` -> `LogRecordAttributeValueLengthLimit` cascade chain | DirectProperty | Class-level `IDisposable` snapshot/restore (no env vars set in this test) |
| `SdkLimitOptionsTests.SpanAttributeCountLimitFallback` | `AttributeCountLimit` -> `SpanAttributeCountLimit` -> `SpanEventAttributeCountLimit` / `SpanLinkAttributeCountLimit` / `LogRecordAttributeCountLimit` cascade chain | DirectProperty | Class-level `IDisposable` snapshot/restore |
| `SdkLimitOptionsTests.SdkLimitOptionsUsingIConfiguration` | All ten properties bound from `AddInMemoryCollection` `IConfiguration` | DirectProperty | Class-level `IDisposable` snapshot/restore (no env vars set) |

**Note:** The class does not carry `[Collection]` attribute. Env-var
isolation depends solely on the class-level IDisposable pattern. This
is adequate for the current test set (env vars are cleared in the
constructor and restored on dispose) but means the class runs in
parallel with other non-collection-attributed tests.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Constructor env-var reads per property

The internal constructor reads all ten env vars via `SetIntConfigValue`.
The public parameterless constructor wraps a fresh env-var-backed
`IConfiguration` and delegates.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT` read and stored in `AttributeValueLengthLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed to `int?`; stored | covered |
| `OTEL_ATTRIBUTE_COUNT_LIMIT` read and stored in `AttributeCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; default 128 if absent | covered |
| `OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT` -> `SpanAttributeValueLengthLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; sets `spanAttributeValueLengthLimitSet = true` | covered |
| `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT` -> `SpanAttributeCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; sets `spanAttributeCountLimitSet = true` | covered |
| `OTEL_SPAN_EVENT_COUNT_LIMIT` -> `SpanEventCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; default 128 if absent | covered |
| `OTEL_SPAN_LINK_COUNT_LIMIT` -> `SpanLinkCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; default 128 if absent | covered |
| `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT` -> `SpanEventAttributeCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; sets `spanEventAttributeCountLimitSet = true` | covered |
| `OTEL_LINK_ATTRIBUTE_COUNT_LIMIT` -> `SpanLinkAttributeCountLimit` | `SdkLimitOptionsIsInitializedFromEnvironment` | Parsed; sets `spanLinkAttributeCountLimitSet = true` | covered |
| `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT` -> `LogRecordAttributeValueLengthLimit` | `SdkLimitOptionsUsingIConfiguration` (IConfiguration only; not env var) | Parsed; sets `logRecordAttributeValueLengthLimitSet = true` | partial (only IConfiguration path tested; direct env var path not tested) |
| `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT` -> `LogRecordAttributeCountLimit` | `SdkLimitOptionsUsingIConfiguration` (IConfiguration only; not env var) | Parsed; no hardcoded default | partial (only IConfiguration path tested; direct env var path not tested) |
| Invalid (non-numeric) env var value rejected; default kept | - | `SetIntConfigValue` calls `TryGetIntValue`; on parse failure the setter is not called; if a `defaultValue` was supplied that is used instead | missing |

### 2.2 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All properties at their constructor defaults (no env vars, no config) | `SdkLimitOptionsDefaults` | `AttributeValueLengthLimit = null`; `AttributeCountLimit = 128`; all cascade properties reflect the base values | covered |
| Stable snapshot of the full default shape | - | Not snapshotted | missing (candidate for snapshot-library pilot after `ExperimentalOptions`) |

### 2.3 Cascade behaviour (current constructor-time implementation)

The cascade is read-time: the getter checks the `*Set` flag and
delegates to the parent property if not set. All tests below use direct
property mutation without a DI pipeline.

**Important baseline note:** The current cascade tests (`SpanAttributeValueLengthLimitFallback`,
`SpanAttributeCountLimitFallback`) exercise cascade on a manually
constructed options object. They do **not** test what the cascade
returns when an env var sets only the parent key and the
child-specific key is absent, because that scenario is exercised
through the constructor. Tests that combine env-var reads with cascade
observation are in the partial category below.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `SpanAttributeValueLengthLimit` returns `AttributeValueLengthLimit` when not explicitly set | `SpanAttributeValueLengthLimitFallback` | Getter returns parent value | covered |
| `SpanAttributeValueLengthLimit` returns its own value when explicitly set | `SpanAttributeValueLengthLimitFallback` | Getter returns own backing field | covered |
| `SpanAttributeValueLengthLimit` returns `null` when set to `null` after being explicitly set | `SpanAttributeValueLengthLimitFallback` | `spanAttributeValueLengthLimitSet = true`; getter returns `null` | covered |
| `LogRecordAttributeValueLengthLimit` falls back to `AttributeValueLengthLimit` | `SpanAttributeValueLengthLimitFallback` | Getter delegates to `AttributeValueLengthLimit` | covered |
| `SpanAttributeCountLimit` falls back to `AttributeCountLimit` | `SpanAttributeCountLimitFallback` | Getter delegates | covered |
| `SpanEventAttributeCountLimit` falls back through `SpanAttributeCountLimit` -> `AttributeCountLimit` | `SpanAttributeCountLimitFallback` | Three-level chain verified | covered |
| `SpanLinkAttributeCountLimit` falls back through `SpanAttributeCountLimit` -> `AttributeCountLimit` | `SpanAttributeCountLimitFallback` | Three-level chain verified | covered |
| `LogRecordAttributeCountLimit` falls back to `AttributeCountLimit` | `SpanAttributeCountLimitFallback` | Getter delegates | covered |
| Env var sets only `OTEL_ATTRIBUTE_COUNT_LIMIT`; `SpanEventAttributeCountLimit` getter reflects that value through the cascade | - | Constructor sets `AttributeCountLimit = 128`; child `*Set` flags are false; getters cascade | partial (defaults test `SdkLimitOptionsDefaults` proves cascade direction at default; no isolated env-var-only-parent test) |
| Cascade flipped under reload: `PostConfigure<T>` writes derived values into child properties so they are stable after construction | - | Not implemented today; Issue 5 proposes this change | missing (expected to become testable when Issue 5 lands; mark as "baseline = current behaviour absent") |
| `IOptionsMonitor<SdkLimitOptions>.OnChange` fires on `IConfiguration` reload | - | Not wired; `OnChange` callbacks exist on the monitor but no subscriber in the serializer path re-reads the options instance | missing |
| Serializer observes updated limit values after reload (cascade-under-reload) | - | Not observable today; `OtlpTraceExporter` stores `sdkLimitOptions` at construction and never refreshes | missing |

### 2.4 Named-options subsection

N/A - single instance. `SdkLimitOptions` has no named-options
dispatch. Code comments in `OtlpTraceExporterHelperExtensions.cs:86-90`
and `OtlpExporterBuilder.cs:178-181` explicitly document this design
decision. All DI registrations resolve `IOptionsMonitor<SdkLimitOptions>.CurrentValue`
without a name argument.

### 2.5 Invalid-input characterisation

`SetIntConfigValue` calls `configuration.TryGetIntValue` (which emits an
`OpenTelemetryProtocolExporterEventSource.Log` message on parse failure)
and only invokes the setter on success. On failure it falls back to the
supplied `defaultValue` if one is provided, otherwise the property
retains its type default (`null` for `int?`).

| Property | Malformed input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `AttributeValueLengthLimit` | Env var non-numeric string | Parse fails; `setter` not called; property stays `null` (no hardcoded default supplied) | - | missing |
| `AttributeCountLimit` | Env var non-numeric string | Parse fails; falls back to `DefaultSdkLimit = 128` (the hardcoded default passed to `SetIntConfigValue`) | - | missing |
| `SpanAttributeCountLimit` | Env var non-numeric string | Parse fails; setter not called; property stays at cascade fallback | - | missing |
| `SpanEventCountLimit` | Env var non-numeric string | Parse fails; falls back to `DefaultSdkLimit = 128` | - | missing |
| `SpanLinkCountLimit` | Env var non-numeric string | Parse fails; falls back to `DefaultSdkLimit = 128` | - | missing |
| `SpanEventAttributeCountLimit` | Env var non-numeric string | Parse fails; setter not called; cascade active | - | missing |
| `SpanLinkAttributeCountLimit` | Env var non-numeric string | Parse fails; setter not called; cascade active | - | missing |
| `LogRecordAttributeValueLengthLimit` | Env var non-numeric string | Parse fails; setter not called; cascade from `AttributeValueLengthLimit` | - | missing |
| `LogRecordAttributeCountLimit` | Env var non-numeric string | Parse fails; setter not called; cascade from `AttributeCountLimit` | - | missing |
| Any property | Programmatic negative value | Stored as-is; no validation in the options class today; serializer treats value as a count limit without range-checking | - | missing |
| Any property | Programmatic zero value | Stored as-is; serializer would drop all items of that type | - | missing |

All missing rows are expected to change under Issue 1 (add
`IValidateOptions<T>` and `ValidateOnStart`). Tests added here pin
today's silent-accept behaviour so Issue 1 produces a visible delta.

### 2.6 Reload no-op baseline

`SdkLimitOptions` is resolved once at export-pipeline build time
(`IOptionsMonitor<SdkLimitOptions>.CurrentValue`) and stored inside
`OtlpTraceExporter.sdkLimitOptions` and `OtlpLogExporter.sdkLimitOptions`
as a value copy. There is no subscription to `IOptionsMonitor.OnChange`
in either exporter. Reloading `IConfiguration` has no effect on already-
constructed exporters.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<SdkLimitOptions>.OnChange` fires on `IConfigurationRoot.Reload()` | - | The monitor fires; no subscriber reacts | missing |
| Built `OtlpTraceExporter.sdkLimitOptions.SpanAttributeCountLimit` unchanged after reload | - | Unchanged (captured at build time) | missing |
| Built `OtlpLogExporter.sdkLimitOptions.LogRecordAttributeCountLimit` unchanged after reload | - | Unchanged (captured at build time) | missing |

All three rows are expected to flip under Issue 22 (wire `OnChange` for
`SdkLimitsOptions` in OTLP serializers) and Issue 17 (standard
`OnChange` subscriber pattern).

### 2.7 Consumer-observed effects in OTLP serializers

Effects observable only by running the serializer with a constrained
batch and inspecting what is written to the buffer.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `SpanAttributeCountLimit` enforced: attributes beyond limit dropped by `WriteActivityTags` | - | Excess tags increment `DroppedTagCount`; dropped-count field written in proto | missing |
| `AttributeValueLengthLimit` enforced: attribute value truncated in `WriteActivityTags` | - | `ProtobufOtlpTagWriter.TryWriteTag` receives `maxAttributeValueLength` and truncates | missing |
| `SpanEventCountLimit` enforced: events beyond limit dropped by `WriteSpanEvents` | - | Excess events increment `droppedEventCount`; dropped-count field written in proto | missing |
| `SpanEventAttributeCountLimit` enforced in `WriteEventAttributes` | - | Cascade value used as `maxAttributeCount` | missing |
| `SpanLinkCountLimit` enforced in `WriteSpanLinks` | - | Excess links dropped | missing |
| `SpanLinkAttributeCountLimit` enforced in `WriteLinkAttributes` | - | Cascade value used as `maxAttributeCount` | missing |
| `LogRecordAttributeCountLimit` enforced in `WriteLogRecord` | - | Value read as `state.AttributeCountLimit`; `int.MaxValue` if null | missing |
| `LogRecordAttributeValueLengthLimit` enforced in `WriteLogRecord` | - | Value read as `state.AttributeValueLengthLimit` | missing |
| Cascade effect at serializer: only `OTEL_ATTRIBUTE_COUNT_LIMIT` set; `WriteEventAttributes` uses that value | - | `SpanEventAttributeCountLimit` cascades through `SpanAttributeCountLimit` to `AttributeCountLimit` | missing |

---

## 3. Recommendations

One bullet per gap. Test names follow the dominant
`Subject_Condition_Expected` convention from the Session 0a naming
survey. Tier mapping per entry-doc Section 3. Observation-mechanism
labels match Section 2 of the entry doc. All rows describe tests to be
**planned** - no test code is written in this cycle.

### 3.1 Env-var read completeness (missing logrecord env vars)

1. **`SdkLimitOptions_LogRecordAttributeValueLengthLimit_EnvVarRead`**
   (new test in `SdkLimitOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty - set
     `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT` in the env and
     construct a `new SdkLimitOptions()`. Assert
     `options.LogRecordAttributeValueLengthLimit == <value>`.
   - Guards Issue 1. Risks pinned: none class-specific.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour. No planned change.
     // Observation: DirectProperty - env var read via public parameterless ctor.
     // Coverage index: sdk-limit-options.logrecord-attribute-value-length-limit.env-var
     ```

   - Risk vs reward: trivial to write; closes the only env-var path
     not covered in `SdkLimitOptionsIsInitializedFromEnvironment`
     (that test does not set the logrecord env vars).

2. **`SdkLimitOptions_LogRecordAttributeCountLimit_EnvVarRead`** (same
   file and tier; symmetrical to the above for
   `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT`). Guards Issue 1.

### 3.2 Invalid-input characterisation (guards Issue 1)

1. **`SdkLimitOptions_InvalidEnvVar_NonNumeric_FallsBackToDefault`**
   (new; `SdkLimitOptionsTests.cs`). Tier 1. Mechanism: DirectProperty.
   Set `OTEL_ATTRIBUTE_COUNT_LIMIT = "notanumber"` and construct a `new
   SdkLimitOptions()`. Assert `options.AttributeCountLimit == 128`
   (the hardcoded `DefaultSdkLimit`).
   - Guards Issues 1, 6 (`TryGetIntValue` logs on parse failure).
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current behaviour.
     // Expected to change under Issue #1 (IValidateOptions<T> + ValidateOnStart).
     // Guards risks: none class-specific.
     // Observation: DirectProperty - silent fallback to DefaultSdkLimit.
     // Coverage index: sdk-limit-options.attribute-count-limit.invalid-input
     ```

   - Risk vs reward: low effort; pins silent fallback so Issue 1 has
     a visible delta.

2. **`SdkLimitOptions_InvalidEnvVar_NonNumeric_NullablePropertyStaysNull`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Set
   `OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT = "notanumber"` (no hardcoded
   default) and assert
   `options.AttributeValueLengthLimit == null`. Guards Issue 1.

3. **`SdkLimitOptions_NegativeValue_IsAcceptedSilently`** (new; same
   file). Tier 1. Mechanism: DirectProperty on a directly constructed
   options object. Set `options.AttributeCountLimit = -1`. Assert it is
   stored as-is (`== -1`). Code-comment: "BASELINE: pins silent accept;
   expected to change under Issue 1." Guards Issue 1.

4. **`SdkLimitOptions_ZeroValue_IsAcceptedSilently`** (new; same file).
   Tier 1. Mechanism: DirectProperty. Sets `AttributeCountLimit = 0`.
   Guards Issue 1.

### 3.3 Cascade-under-env-var scenario

1. **`SdkLimitOptions_CascadeFromEnvVarParentOnly`** (new;
   `SdkLimitOptionsTests.cs`). Tier 1. Mechanism: DirectProperty.
   Set only `OTEL_ATTRIBUTE_COUNT_LIMIT = 50` (no span-specific vars).
   Construct `new SdkLimitOptions()`. Assert:
   `options.SpanAttributeCountLimit == 50`,
   `options.SpanEventAttributeCountLimit == 50`,
   `options.SpanLinkAttributeCountLimit == 50`,
   `options.LogRecordAttributeCountLimit == 50`. This pins the
   constructor-time cascade for the env-var path, not just for
   programmatic mutation.
   - Guards Issues 1, 5.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins current constructor-time cascade.
     // Expected to change under Issue #5 (cascade moves to PostConfigure<T>).
     // Observation: DirectProperty - cascade via property getters, not DI pipeline.
     // Coverage index: sdk-limit-options.attribute-count-limit.env-var-cascade
     ```

   - Risk vs reward: low effort; is the highest-value baseline for
     Issue 5 because it pins the "current cascade is constructor-time"
     fact in a way that will fail if Issue 5 changes the cascade path.

2. **`SdkLimitOptions_CascadeFromEnvVarParent_ChildEnvVarOverrides`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Set
   `OTEL_ATTRIBUTE_COUNT_LIMIT = 50` and
   `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT = 99`. Assert
   `SpanEventAttributeCountLimit == 99` (own value wins) and
   `SpanLinkAttributeCountLimit == 50` (cascade from parent). Guards
   Issues 1, 5.

### 3.4 Cascade-under-reload (cascade-flipped-under-reload baseline)

All rows below are **missing** because the current implementation stores
`SdkLimitOptions` as a value-copy at exporter construction. These tests
exist to pin the no-op reload baseline so Issue 22 has a visible delta.

1. **`OtlpTraceExporter_SdkLimitOptions_ReloadDoesNotChangeStoredLimits`**
   (new; `OtlpTraceExporterHelperExtensionsTests.cs` or a new
   `SdkLimitOptionsReloadTests.cs` in OTPT). Tier 2. Mechanism: DI
   + InternalAccessor (access `OtlpTraceExporter.sdkLimitOptions` via
   `InternalsVisibleTo`; the field is already accessible from
   `OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests`). Build a
   provider with `AddOtlpExporter` and an in-memory `IConfiguration`
   containing `OTEL_ATTRIBUTE_COUNT_LIMIT = 50`. Resolve the
   `OtlpTraceExporter` instance. Call `IConfigurationRoot.Reload()` after
   updating the in-memory source to 99. Assert
   `exporter.sdkLimitOptions.AttributeCountLimit == 50` (unchanged).
   - Guards Issues 22, 17.
   - Code-comment hint:

     ```csharp
     // BASELINE: pins no-op reload.
     // Expected to change under Issue #22 (OnChange wiring for SdkLimitOptions in serializers).
     // Observation: InternalAccessor - reads OtlpTraceExporter.sdkLimitOptions (internal field).
     // Coverage index: sdk-limit-options.all-properties.reload-no-op
     ```

   - Risk vs reward: moderate setup; high value - without this, Issue
     22 has no visible test delta when it lands. Brittleness: medium
     (internal field access); acceptable given `InternalsVisibleTo`
     is already wired.

2. **`OtlpLogExporter_SdkLimitOptions_ReloadDoesNotChangeStoredLimits`**
   (new; same file as above or alongside). Tier 2. Mechanism and
   structure identical to the tracing variant; reads
   `OtlpLogExporter.sdkLimitOptions.LogRecordAttributeCountLimit`.
   Guards Issues 22, 17.

3. **`SdkLimitOptions_OnChangeSubscription_FiresOnReload`** (new; same
   file). Tier 2. Mechanism: DI. Register
   `IOptionsMonitor<SdkLimitOptions>.OnChange`. Trigger
   `IConfigurationRoot.Reload()`. Assert the callback fires. Does NOT
   assert that the exporter reacts (that is Issue 22's job). Pins that
   the monitor notification path is live. Guards Issue 17.

### 3.5 Consumer-observed serializer effects

These tests require constructing real `Activity` or `LogRecord` batches
and inspecting the serialized output. All are Tier 2 because they
require a DI-built exporter or direct serializer invocation with a
controlled batch.

Observation mechanism for this group: **Mock** - invoke the serializer
directly (its methods are `internal static`) with a test buffer and a
constructed `SdkLimitOptions` instance; inspect the resulting byte range
or use `ProtobufHelper` to decode the proto. The serializer methods are
`internal` and accessible via `InternalsVisibleTo`.

1. **`ProtobufOtlpTraceSerializer_WriteActivityTags_EnforcesAttributeCountLimit`**
   (new; a `ProtobufOtlpTraceSerializerTests.cs` or alongside
   `OtlpTraceExporterTests.cs`). Tier 2. Mechanism: InternalAccessor
   (direct call to `ProtobufOtlpTraceSerializer.WriteActivityTags`).
   Pass a `SdkLimitOptions` with `AttributeCountLimit = 2` and an
   `Activity` with 5 tags. Assert the written proto contains exactly
   2 attribute entries and a non-zero `DroppedAttributesCount` field.
   - Guards Issues 1, 22. Risks pinned: none.
   - Code-comment hint: "BASELINE: pins SpanAttributeCountLimit
     enforcement at serializer. No planned change to the enforcement;
     change expected to cascade path only under Issue 5."
   - Risk vs reward: moderate setup; the serializer methods are
     already unit-tested for correctness; this test specifically
     pins the limit enforcement so a regressor shows up as a test
     failure rather than silent data truncation.

2. **`ProtobufOtlpTraceSerializer_WriteSpanEvents_EnforcesEventCountLimit`**
   (new; same location). Tier 2. Mechanism: InternalAccessor. Pass
   `SpanEventCountLimit = 1` with 3 events. Assert 1 event written and
   `DroppedEventsCount` field present. Guards Issues 1, 22.

3. **`ProtobufOtlpTraceSerializer_WriteEventAttributes_EnforcesEventAttributeCountLimit`**
   (new; same location). Tier 2. Mechanism: InternalAccessor. Pass
   `SpanEventAttributeCountLimit = 1` with an event with 3 attributes.
   Guards Issues 1, 5, 22 (cascade chain `SpanEventAttributeCountLimit ->
   SpanAttributeCountLimit -> AttributeCountLimit` tested in Section
   3.3; this test pins the serializer enforcement path).

4. **`ProtobufOtlpTraceSerializer_WriteSpanLinks_EnforcesLinkCountLimit`**
   and
   **`ProtobufOtlpTraceSerializer_WriteLinkAttributes_EnforcesLinkAttributeCountLimit`**
   (new; same location). Tier 2. Mechanism: InternalAccessor. Guards
   Issues 1, 22.

5. **`ProtobufOtlpLogSerializer_WriteLogRecord_EnforcesAttributeCountLimit`**
   (new; a `ProtobufOtlpLogSerializerTests.cs` or alongside
   `OtlpLogExporterTests.cs`). Tier 2. Mechanism: InternalAccessor.
   Pass `LogRecordAttributeCountLimit = 2` with a `LogRecord` carrying 4
   attributes. Assert 2 attributes written. Guards Issues 1, 22.

6. **`ProtobufOtlpLogSerializer_WriteLogRecord_EnforcesAttributeValueLengthLimit`**
   (new; same location). Tier 2. Mechanism: InternalAccessor. Pass
   `LogRecordAttributeValueLengthLimit = 5` with a `LogRecord` attribute
   with a 20-character value. Assert the written attribute value is
   truncated. Guards Issues 1, 22.

7. **`ProtobufOtlpTraceSerializer_WriteActivityTags_CascadeEnforcedAtSerializer`**
   (new; same location). Tier 2. Mechanism: InternalAccessor. Construct
   a `SdkLimitOptions` with only `AttributeCountLimit = 3` set (no
   span-specific override). Assert the same 3-attribute enforcement
   applies in `WriteActivityTags`. This is the "cascade-observed-at-
   serializer" test that complements the property-level cascade tests in
   Section 3.3.
   - Guards Issues 1, 5 (if Issue 5 changes how the cascade is
     computed, this test detects that the serializer receives the
     correct effective value).
   - Code-comment hint: "BASELINE: pins that SpanAttributeCountLimit
     getter cascades to AttributeCountLimit at the point the
     serializer reads it."

### 3.6 DI-resolved default-state and isolation

1. **`SdkLimitOptions_ResolvedViaDi_HasExpectedDefaults`** (new;
   `SdkLimitOptionsTests.cs` or a dedicated DI test). Tier 2. Mechanism:
   DI (`IServiceProvider.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>()`
   after calling `services.AddOtlpExporter(...)` to trigger the factory
   registration). Assert default property values through DI rather than
   direct construction. Closes the gap between the current direct-
   construction defaults test and what the DI pathway actually produces.
   - Guards Issues 1, 5, 10.
   - Code-comment hint: "BASELINE: pins factory-produced defaults
     through OtlpServiceCollectionExtensions registration."
   - Risk vs reward: low brittleness; medium effort; high value for
     Issue 10 (moving `SdkLimitOptions` to a public class in core SDK
     will require this DI path to still work).

### 3.7 Default-state snapshot (pilot-dependent)

1. **`SdkLimitOptions_Default_Snapshot`** (new; `SdkLimitOptionsTests.cs`
   or `Snapshots/` subfolder). Tier 1. Mechanism: Snapshot (library
   TBD by maintainers; see entry-doc Appendix A). Requires the snapshot-
   library pilot to complete before adoption.
   - Guards Issues 1, 5, 10.
   - Code-comment hint: "BASELINE: pins whole-options shape. Snapshot
     update expected on any additive change."
   - Risk vs reward: deferred until pilot on `ExperimentalOptions`
     confirms the CI diff workflow. Low per-run cost; high value for
     catching silent default drift across Issues 5 and 10.

### Prerequisites and dependencies

- 3.1 through 3.3 depend on the env-var isolation pattern decision
  (entry-doc Section 5); the existing class-level `IDisposable` pattern
  in `SdkLimitOptionsTests` can be reused for all Tier 1 tests.
- 3.4 depends on the reload pathway file
  (`../pathways/reload-no-op-baseline.md`) landing first so the reload
  tests share the standard reload harness.
- 3.5 tests require `InternalsVisibleTo` from `SdkLimitOptions.cs` to
  the OTLP test project; this wiring already exists
  (`InternalsVisibleTo` from `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/`
  to `OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests` - confirmed in
  Session 0a Sec.4.G).
- 3.6 depends on `AddOtlpExporter` being callable in the test with no
  live export endpoint; mock transport infrastructure from Session 0a
  Sec.4.E (`TestExportClient`) is sufficient.
- 3.7 depends on snapshot-library selection (entry-doc Appendix A).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.5, 3.6.
- **Issue 5** - Move `SdkLimitOptions` fallback cascade logic to
  `PostConfigure<T>`. Guarded by: Sections 3.3, 3.4, 3.5.
- **Issue 6** - Add diagnostic logging for `RegisterOptionsFactory`
  silent skip. Guarded by: Section 3.2.1 (`TryGetIntValue` logs on
  parse failure; pinning the silent fallback also pins the log event
  path).
- **Issue 10** - Add public `SdkLimitsOptions` to core SDK package.
  Guarded by: Section 3.6 (DI-resolved defaults test pins the
  registration path that Issue 10 will restructure).
- **Issue 17** - Design and implement standard `OnChange` subscriber
  pattern. Guarded by: Section 3.4.
- **Issue 22** - Wire `OnChange` for `SdkLimitsOptions` in OTLP
  serializers. Guarded by: Sections 3.4, 3.5.

Reciprocal "Baseline tests required" lines should be added to each of
the issues above, citing this file. Those edits happen in the final
cross-reference sweep, not here.
