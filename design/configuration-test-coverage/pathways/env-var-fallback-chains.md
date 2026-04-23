# Env-Var Fallback Chains - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:29` -
  constructor reads ten env-var keys via `SetIntConfigValue`, which calls the
  property setter only when the key is present or a hard-coded default exists.
  Fallback is implemented via property getters using `*Set` boolean flags:
  `SpanAttributeValueLengthLimit.get` returns `AttributeValueLengthLimit` when
  the span-specific setter has not been called; `SpanEventAttributeCountLimit.get`
  falls back through `SpanAttributeCountLimit.get` which itself falls back to
  `AttributeCountLimit`. All fallback evaluation is lazy (getter-time, not
  constructor-time), but the `*Set` flags are locked by the constructor.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:253` -
  `ApplyConfiguration` dispatches to `ApplyConfigurationUsingSpecificationEnvVars`
  with the env-var key set appropriate to `OtlpExporterOptionsConfigurationType`.
  Signal-specific and generic keys are read in separate constructor calls; there
  is no per-property fallback within a single call. The fallback from
  signal-specific to generic is implemented instead by
  `ApplyDefaults(OtlpExporterOptions defaultExporterOptions)` (line 234): each
  nullable backing field is set to the default instance's field via `??=` if it
  was not populated by the signal-specific constructor call.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs` -
  all sixteen signal-specific and generic OTLP env-var key constants.
  Signal-specific keys: `TracesEndpointEnvVarName`, `LogsEndpointEnvVarName`,
  `MetricsEndpointEnvVarName`, and their `-HEADERS`, `-TIMEOUT`, `-PROTOCOL`
  siblings. Generic keys: `DefaultEndpointEnvVarName` etc.
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTraceExporterHelperExtensions.cs:62` -
  `AddOtlpExporter` always constructs with `OtlpExporterOptionsConfigurationType.Default`;
  signal-specific keys are never read in this pathway.

Fallback chain summary:

```
SdkLimitOptions property getters (lazy, locked by ctor *Set flags):
  SpanEventAttributeCountLimit -> SpanAttributeCountLimit -> AttributeCountLimit
  SpanLinkAttributeCountLimit  -> SpanAttributeCountLimit -> AttributeCountLimit
  SpanAttributeValueLengthLimit -> AttributeValueLengthLimit
  LogRecordAttributeValueLengthLimit -> AttributeValueLengthLimit
  LogRecordAttributeCountLimit -> AttributeCountLimit

OtlpExporterOptions (two-tier, constructor + ApplyDefaults):
  OtlpExporterOptionsConfigurationType.Traces  (reads OTEL_EXPORTER_OTLP_TRACES_*)
    |
    v ApplyDefaults (??= pattern, field not null check)
  OtlpExporterOptionsConfigurationType.Default (reads OTEL_EXPORTER_OTLP_*)
```

## 1. Existing coverage

Section 1 is facts-only; no gap marking.

| File:method | Scenario summary | Observation | Env-var isolation |
| --- | --- | --- | --- |
| `SdkLimitOptionsTests.SpanAttributeValueLengthLimitFallback` | `AttributeValueLengthLimit` falls back to `SpanAttributeValueLengthLimit`; `LogRecordAttributeValueLengthLimit` falls back to `AttributeValueLengthLimit` | DirectProperty | Class-IDisposable |
| `SdkLimitOptionsTests.SpanAttributeCountLimitFallback` | `AttributeCountLimit` -> `SpanAttributeCountLimit` -> `SpanEventAttributeCountLimit` -> `SpanLinkAttributeCountLimit` cascade | DirectProperty | Class-IDisposable |
| `SdkLimitOptionsTests.SdkLimitOptionsIsInitializedFromEnvironment` | All env vars read; each property reflects the env-var value | DirectProperty | Class-IDisposable |
| `OtlpExporterOptionsTests.OtlpExporterOptions_EnvironmentVariableOverride` | Signal-specific and generic env vars applied (Theory over signal types) | DirectProperty | Class-IDisposable+[Collection] |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsTest` | All `OTEL_EXPORTER_OTLP_*` env vars for all signals in UseOtlpExporter | DI | Class-IDisposable+[Collection] |

Observations:

- `SpanAttributeValueLengthLimitFallback` and `SpanAttributeCountLimitFallback`
  verify the cascade via direct construction with no env vars set (they use
  the IConfiguration ctor and set properties programmatically). These tests
  cover the getter-fallback logic but not the env-var-locked-then-Configure<T>
  scenario.
- `OtlpExporterOptions_EnvironmentVariableOverride` covers the signal-specific
  path (via `OtlpExporterOptionsConfigurationType` Theory) but does not cover
  the two-tier fallback (signal-specific env var absent, generic env var present).
- `UseOtlpExporterRespectsSpecEnvVarsTest` covers all three signals and all four
  generic properties but focuses on presence of signal-specific values; it does
  not explicitly test the fallback path when only the generic variable is set.

## 2. Scenario checklist and gap analysis

### 2.1 SdkLimitOptions - cascade getter behavior

The `*Set` boolean flags are set to `true` whenever the property setter is
called, whether from a constructor env-var read, a direct assignment, or a
`Configure<T>` delegate. Once `spanAttributeCountLimitSet = true`, the getter
for `SpanAttributeCountLimit` no longer falls back to `AttributeCountLimit`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Getter cascade when no specific env var set (generic falls through) | `SpanAttributeCountLimitFallback` | Generic value returned via getter fallback | covered |
| Getter cascade when specific env var set (specific wins, generic ignored) | `SdkLimitOptionsIsInitializedFromEnvironment` (partial) | `*Set = true`; specific value returned | partial |
| Configure<T> sets generic after constructor locked specific from env var | none | Specific retains env-var value; generic change does not propagate | missing |
| Configure<T> sets specific after constructor | none | `*Set = true` from delegate; delegate value wins | missing |
| Cascade for LogRecord properties (two levels) | `SpanAttributeValueLengthLimitFallback` | `LogRecordAttributeValueLengthLimit` falls back to `AttributeValueLengthLimit` | covered |
| `LogRecordAttributeCountLimit` falls back to `AttributeCountLimit` | none | Getter fallback expected; not explicitly tested | missing |

The partial rating for the second row: `SdkLimitOptionsIsInitializedFromEnvironment`
sets ALL env vars and asserts each property; it incidentally exercises
`spanAttributeCountLimitSet = true` but does not also set the generic limit to a
different value to prove the specific wins over the generic.

### 2.2 SdkLimitOptions - Configure<T> after constructor locked specific from env var

This is the core Issue 5 scenario. The cascade flags are set in the constructor
when an env var is present. A `Configure<T>` delegate that modifies only the
generic property (`AttributeCountLimit`) cannot propagate its new value through
to specific properties whose `*Set` flag was set to `true` during construction.

Example: `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT=50` set as env var. Constructor runs:
`spanAttributeCountLimitSet = true`, `spanAttributeCountLimit = 50`.
Later, `Configure<SdkLimitOptions>(opts => opts.AttributeCountLimit = 200)` runs.
`AttributeCountLimit` is now 200. But `SpanAttributeCountLimit.get` returns 50
because `spanAttributeCountLimitSet` remains `true`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Configure<T> sets generic; specific locked by env var - specific wins | none | Getter returns env-var-sourced specific value; generic change ignored | missing |
| Configure<T> sets specific directly; overrides env-var value | none | Setter called; `*Set = true`; Configure<T> value wins | missing |
| PostConfigure sets cascade (expected behavior post-Issue 5) | none | PostConfigure step absent today; this is the gap Issue 5 addresses | missing |

All three rows are `missing`. The first two must exist as baseline tests before
Issue 5 changes the cascade placement, so the behavioral delta from moving the
cascade to PostConfigure is visible.

### 2.3 OTLP signal-specific to generic fallback - UseOtlpExporter pathway

In `UseOtlpExporter`, `OtlpExporterBuilderOptions` constructs three
signal-specific `OtlpExporterOptions` instances and calls `ApplyDefaults` to
merge generic values where signal-specific fields are null. Each field uses
`??=`: if the signal-specific constructor populated the field, the generic
value is ignored; if the field remains null after the signal-specific
constructor, it is filled from the generic instance.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Signal-specific endpoint env var present - used, generic ignored | `UseOtlpExporterRespectsSpecEnvVarsTest` | Signal-specific wins | covered |
| Only generic endpoint env var present - used as fallback for all signals | `UseOtlpExporterRespectsSpecEnvVarsTest` (implicit - no signal-specific set) | Generic used via ApplyDefaults | partial |
| Both generic and signal-specific set - signal-specific wins (traces) | none | `??=` pattern; signal-specific field non-null, generic ignored | missing |
| Both set - signal-specific wins (metrics) | none | Same mechanism | missing |
| Both set - signal-specific wins (logs) | none | Same mechanism | missing |
| Generic fallback for timeout and headers (not just endpoint) | none | Same `ApplyDefaults` `??=` pattern covers all four fields | missing |

The partial rating for the second row: `UseOtlpExporterRespectsSpecEnvVarsTest`
sets all signal-specific env vars; the generic fallback path fires only when
signal-specific vars are absent, and the test does not isolate that branch.

### 2.4 OTLP AddOtlpExporter pathway - no signal-specific fallback

`AddOtlpExporter` always uses `OtlpExporterOptionsConfigurationType.Default`;
signal-specific env vars are not read. The fallback chain does not apply. Setting
`OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` when using `AddOtlpExporter` for traces has
no effect; the generic `OTEL_EXPORTER_OTLP_ENDPOINT` is used instead.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Generic endpoint env var applied in AddOtlpExporter (traces) | `OtlpExporterOptions_EnvironmentVariableOverride` (Default type row) | Generic applied | covered |
| Signal-specific env var silently ignored in AddOtlpExporter (traces) | none | Key not read; default endpoint used | missing |
| Generic endpoint env var applied in AddOtlpExporter (metrics, logs) | `OtlpExporterOptions_EnvironmentVariableOverride` (Default type row, Theory) | Generic applied for all signals | covered |

## 3. Recommendations

### R1: Configure<T> sets generic after constructor locked specific - baseline

- **Target test:**
  `SdkLimitOptions_ConfigureT_SetsGeneric_AfterCtorLockedSpecificFromEnvVar`
- **Location:** `SdkLimitOptionsTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - inject IConfiguration with
  `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT=50`. Register
  `Configure<SdkLimitOptions>(opts => opts.AttributeCountLimit = 200)`. Resolve
  `IOptions<SdkLimitOptions>`. Assert `SpanAttributeCountLimit == 50` (env var
  wins because `*Set = true`). Assert `AttributeCountLimit == 200` (Configure<T>
  applied to the simple property).
- **Guards issues:** 5, 10
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #5 (PostConfigure cascade for SdkLimitOptions;
// after the move the generic Configure<T> value should propagate through
// the cascade to SpanAttributeCountLimit when no signal-specific override
// was provided).
// Guards risks: 1.1.
// Observation: DI - IOptions<SdkLimitOptions>; assert specific retains env-var value.
// Coverage index: pathway.env-var-fallback-chains.sdk-limits.configure-t-after-ctor-locked
```

- **Risk vs reward:** Low effort. Highest guard value in this pathway: directly
  pins the behavior that changes when Issue 5 moves the cascade to PostConfigure.
  Without this test the Issue 5 change has no red-green signal in CI.

### R2: Configure<T> sets specific property directly - overrides env var

- **Target test:**
  `SdkLimitOptions_ConfigureT_SetsSpecific_OverridesEnvVar`
- **Location:** `SdkLimitOptionsTests.cs`
- **Tier:** 2
- **Observation:** `DI` - inject IConfiguration with
  `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT=50`. Register
  `Configure<SdkLimitOptions>(opts => opts.SpanAttributeCountLimit = 300)`.
  Resolve `IOptions<SdkLimitOptions>`. Assert `SpanAttributeCountLimit == 300`
  (Configure<T> wins by calling the setter, overwriting the env-var value).
- **Guards issues:** 5, 10
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - Configure<T> calls the specific property setter; *Set = true;
// delegate value wins over env-var-sourced value from constructor.
// Coverage index: pathway.env-var-fallback-chains.sdk-limits.configure-t-specific
```

- **Risk vs reward:** Low effort. Companion to R1; together the two tests bracket
  the full Configure<T> behavior for `SdkLimitOptions` fallback chains.

### R3: SdkLimitOptions LogRecordAttributeCountLimit fallback to AttributeCountLimit

- **Target test:**
  `SdkLimitOptions_LogRecordAttributeCountLimit_FallsBackTo_AttributeCountLimit`
- **Location:** `SdkLimitOptionsTests.cs`
- **Tier:** 1 (direct construction)
- **Observation:** `DirectProperty` - construct with `AttributeCountLimit = 64`
  (no `LogRecordAttributeCountLimit` set). Assert
  `LogRecordAttributeCountLimit == 64`. Then set `LogRecordAttributeCountLimit = 32`
  and assert it returns 32 (specific wins over generic).
- **Guards issues:** 5, 10
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #10 (public SdkLimitOptions; cascade logic
// may move to new public class).
// Guards risks: 1.1.
// Observation: DirectProperty - LogRecord cascade not yet covered.
// Coverage index: pathway.env-var-fallback-chains.sdk-limits.logrecord-count-fallback
```

- **Risk vs reward:** Very low effort (Tier 1). Closes a gap identified in
  Sec.2.1 for the LogRecord cascade branch.

### R4: Both signal-specific and generic OTLP env vars set - signal-specific wins

- **Target test:**
  `UseOtlpExporter_SignalSpecificEndpoint_BeatsGeneric_WhenBothSet` (Theory over
  traces/metrics/logs)
- **Location:** `UseOtlpExporterExtensionTests.cs`
  (`test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`)
- **Tier:** 2
- **Observation:** `DI` - inject IConfiguration with both
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://generic/` and
  `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://traces/` (and equivalents for the
  other two signals). Resolve `OtlpExporterBuilderOptions` and assert each
  signal's effective endpoint matches the signal-specific URL.
- **Guards issues:** 5, 10
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #10 (public SdkLimitOptions; the two-tier
// OTLP fallback is in OtlpExporterOptions and may be affected by the same
// refactor).
// Guards risks: 1.1.
// Observation: DI - OtlpExporterBuilderOptions per-signal effective endpoint.
// Coverage index: pathway.env-var-fallback-chains.otlp-signal.signal-specific-beats-generic
```

- **Risk vs reward:** Low-to-medium effort (Theory over three signals). Directly
  exercises the `ApplyDefaults` `??=` fallback path that is not yet tested with
  both sources present.

### R5: Generic fallback when signal-specific absent - explicit coverage

- **Target test:**
  `UseOtlpExporter_GenericEndpoint_UsedAsFallback_WhenSignalSpecificAbsent`
  (Theory over traces/metrics/logs)
- **Location:** `UseOtlpExporterExtensionTests.cs`
- **Tier:** 2
- **Observation:** `DI` - inject IConfiguration with only
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://generic/` (no signal-specific vars).
  Resolve `OtlpExporterBuilderOptions`. Assert all three signal-specific
  effective endpoints equal `http://generic/`.
- **Guards issues:** 5, 10
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DI - OtlpExporterBuilderOptions ApplyDefaults fills from generic
// when signal-specific constructor left the field null.
// Coverage index: pathway.env-var-fallback-chains.otlp-signal.generic-fallback-use-otlp
```

- **Risk vs reward:** Low effort. Explicitly validates the `??=` path in
  `ApplyDefaults` that `UseOtlpExporterRespectsSpecEnvVarsTest` currently only
  covers by implication.

### R6: Signal-specific env var silently ignored in AddOtlpExporter

- **Target test:**
  `OtlpExporterOptions_SignalSpecificEnvVar_Ignored_In_AddOtlpExporterPathway`
- **Location:** `OtlpExporterOptionsTests.cs`
- **Tier:** 1
- **Observation:** `DirectProperty` - construct `OtlpExporterOptions` directly
  with `OtlpExporterOptionsConfigurationType.Default` and an IConfiguration
  containing `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://traces/`. Assert the
  endpoint returns the default (the signal-specific key is not read in Default
  mode).
- **Guards issues:** 5
- **Risks pinned:** 1.1
- **Code-comment hint:**

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: DirectProperty - Default configurationType ignores signal-specific
// env-var keys; only OTEL_EXPORTER_OTLP_ENDPOINT is read.
// Coverage index: pathway.env-var-fallback-chains.otlp-signal.add-otlp-no-signal-specific
```

- **Risk vs reward:** Very low effort (Tier 1 direct construction). Documents
  the AddOtlpExporter limitation that surprises users who set signal-specific
  vars and use the per-signal `AddOtlpExporter` helper.

## Guards issues

- **Issue 5** - PostConfigure cascade for `SdkLimitOptions`: the cascade logic
  (`SpanAttributeCountLimit` falling back to `AttributeCountLimit`, etc.) is
  proposed to move from the constructor to a `PostConfigure<SdkLimitOptions>`
  registration. Tests R1 and R2 are the direct baselines: they will change from
  green to red when the move happens, signaling which behavior was altered.
  Issue 5 must reference these tests in its implementation checklist.
- **Issue 10** - Public `SdkLimitOptions`: if the class is made public as part
  of the SDK limits surface, the cascade logic will move to a new public class.
  Tests R1-R3 are the baselines for the cascade behavior that must be preserved
  (or deliberately changed) during the move.
