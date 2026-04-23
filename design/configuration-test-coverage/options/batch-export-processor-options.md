# BatchExportProcessorOptions\<T\> - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

`BatchExportProcessorOptions<T>` is an abstract base class. Behavioural
coverage is inherited via the two derived classes and is documented in the
sibling files. This file pins only the base-class surface: property
declarations, default-constant sourcing, generic-type behaviour, and the
validation surface that lives in `BatchExportProcessor<T>` (the consumer,
not the options class itself). Scenarios fully owned by the derived classes
are cross-referenced to the sibling files and are not duplicated here.

Sibling files (Batch 2):

- [`batch-export-activity-processor-options.md`](batch-export-activity-processor-options.md)
- [`batch-export-logrecord-processor-options.md`](batch-export-logrecord-processor-options.md)

---

## Source citations

- Base options class declaration (generic `T : class` constraint, four
  auto-properties with `BatchExportProcessor<T>` constant initialisers) -
  `src/OpenTelemetry/BatchExportProcessorOptions.cs:10-32`.
  - `MaxQueueSize` (`= BatchExportProcessor<T>.DefaultMaxQueueSize`) - line 16.
  - `ScheduledDelayMilliseconds`
    (`= BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds`) - line 21.
  - `ExporterTimeoutMilliseconds`
    (`= BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds`) - line 26.
  - `MaxExportBatchSize` (`= BatchExportProcessor<T>.DefaultMaxExportBatchSize`) -
    line 31.
- No constructor in `BatchExportProcessorOptions<T>`. No env-var reads. No
  `IConfiguration` usage. All env-var reads are in the derived constructors.
- Default constant definitions (`internal const int`) -
  `src/OpenTelemetry/BatchExportProcessor.cs:17-20`.
  - `DefaultMaxQueueSize = 2048` - line 17.
  - `DefaultScheduledDelayMilliseconds = 5000` - line 18.
  - `DefaultExporterTimeoutMilliseconds = 30000` - line 19.
  - `DefaultMaxExportBatchSize = 512` - line 20.
- `BatchExportProcessor<T>` protected constructor (the consumer-side
  validation point; all `Guard.ThrowIfOutOfRange` calls live here) -
  `src/OpenTelemetry/BatchExportProcessor.cs:38-58`.
  - `Guard.ThrowIfOutOfRange(maxQueueSize, min: 1)` - line 46.
  - `Guard.ThrowIfOutOfRange(maxExportBatchSize, min: 1, max: maxQueueSize, ...)` -
    line 47.
  - `Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1)` - line 48.
  - `Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0)` - line 49.
- `BatchExportProcessor<T>` internal observable fields (accessible from
  `OpenTelemetry.Tests` and `OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests`
  via `InternalsVisibleTo`) -
  `src/OpenTelemetry/BatchExportProcessor.cs:22-24`.
  - `internal readonly int MaxExportBatchSize` - line 22.
  - `internal readonly int ScheduledDelayMilliseconds` - line 23.
  - `internal readonly int ExporterTimeoutMilliseconds` - line 24.
- `BatchExportProcessor<T>` internal observable counts -
  `src/OpenTelemetry/BatchExportProcessor.cs:63-73`.
  - `internal long DroppedCount` - line 63 (useful for `MaxQueueSize`
    behavioural observation without private-field reflection).
  - `internal long ReceivedCount` - line 68.
  - `internal long ProcessedCount` - line 73.
- `circularBuffer` private field (carries `maxQueueSize` as capacity; not
  directly observable without reflection) -
  `src/OpenTelemetry/BatchExportProcessor.cs:26`.

### No DI registration

`BatchExportProcessorOptions<T>` is never registered with
`DelegatingOptionsFactory` directly. The two concrete derived classes are
registered instead (one per signal). There are no named-options semantics on
this class.

---

## 1. Existing coverage

Pulled from [`existing-tests.md`](../existing-tests.md). Inventory only.

No test exercises `BatchExportProcessorOptions<T>` directly because the
class is abstract and has no constructor. All existing tests operate on
one of the two derived classes.

### 1.1 Coverage inherited via derived-class tests

The six `BatchExportActivityProcessorOptionsTests` tests and the five
`BatchExportLogRecordProcessorOptionsTests` tests each exercise the base
class indirectly:

- Every derived-class test that constructs an options instance and asserts
  the four default values relies on the base-class property initialisers.
- Every derived-class test that sets a property and asserts its value
  exercises the base-class auto-property setter.

| Inherited from | Tests exercising base-class property surface | Observation mechanism | Details |
| --- | --- | --- | --- |
| `BatchExportActivityProcessorOptionsTests.cs` | 6 tests (see sibling file) | DirectProperty | All four properties read/set; defaults from `BatchExportProcessor<T>` constants |
| `BatchExportLogRecordProcessorOptionsTests.cs` | 5 tests (see sibling file) | DirectProperty | Same surface |
| `OtlpLogExporterTests.AddOtlpExporterSetsDefaultBatchExportProcessor` | 1 test | InternalAccessor | Asserts `ScheduledDelayMilliseconds`, `ExporterTimeoutMilliseconds`, `MaxExportBatchSize` on the built processor |

### 1.2 Base-class-only surface not covered by existing tests

The following aspects belong to `BatchExportProcessorOptions<T>` as a base
class and are not addressed by any existing test:

- Generic-type instantiation with a type parameter other than `Activity` or
  `LogRecord`. The class constraint is `where T : class`; any reference type
  is valid.
- Default-state of the base class verified in isolation (without going via
  a derived type). Not a gap in practice - the derived types are the only
  concrete usage paths.
- Whether the base class's property defaults are stable across different `T`
  type parameters (the initialisers reference `BatchExportProcessor<T>` constants,
  which are typed constants, not instance members; they are the same `int`
  literals regardless of `T`).

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. Rows that are
fully owned by derived classes reference the sibling file and are marked
**covered via derived** to distinguish them from base-class-specific gaps.

### 2.1 Base-class property defaults

The four properties are initialised from `BatchExportProcessor<T>` constants.
Because the constants are the same regardless of `T`, the defaults are
type-parameter-independent.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `MaxQueueSize` default is 2048 (from `DefaultMaxQueueSize`) | Via `BatchExportProcessorOptions_Defaults` in both derived-class tests | 2048 applied by the property initialiser | covered via derived |
| `ScheduledDelayMilliseconds` default is 5000 (from `DefaultScheduledDelayMilliseconds`) | Via `BatchExportProcessorOptions_Defaults` in both derived-class tests | 5000 applied by property initialiser | covered via derived |
| `ExporterTimeoutMilliseconds` default is 30000 (from `DefaultExporterTimeoutMilliseconds`) | Via `BatchExportProcessorOptions_Defaults` in both derived-class tests | 30000 applied by property initialiser | covered via derived |
| `MaxExportBatchSize` default is 512 (from `DefaultMaxExportBatchSize`) | Via `BatchExportProcessorOptions_Defaults` in both derived-class tests | 512 applied by property initialiser | covered via derived |
| Defaults are type-parameter-independent (same constants for `Activity` and `LogRecord`) | Implicitly tested by both derived-class suites; no explicit cross-type assertion | `BatchExportProcessor<T>.Default*` are `internal const int` literals; not per-T | missing (no test asserts the same numeric default for two different `T`) |

The last row is a low-value edge: the constants are `const int` and cannot
vary by `T`. A test would be trivially provable from reading the source.
Maintainers may choose to omit it.

### 2.2 Constructor and env-var read pattern

`BatchExportProcessorOptions<T>` has no constructor and reads no env vars.
All env-var reads are in the derived constructors.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Base class reads no env vars | Not tested explicitly | Class has no constructor; property initialisers use constants | not applicable (structural fact) |
| Env-var reads for `OTEL_BSP_*` | See `batch-export-activity-processor-options.md` Section 2.1 | Implemented in `BatchExportActivityProcessorOptions(IConfiguration)` | covered via derived |
| Env-var reads for `OTEL_BLRP_*` | See `batch-export-logrecord-processor-options.md` Section 2.1 | Implemented in `BatchExportLogRecordProcessorOptions(IConfiguration)` | covered via derived |

### 2.3 Generic-type behaviour

The generic constraint `where T : class` means the class can be instantiated
with any reference type. Only `Activity` and `LogRecord` are used in
production. The property defaults are constant; there is no generic dispatch.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `BatchExportProcessorOptions<Activity>` (via derived class) carries correct defaults | `BatchExportProcessorOptions_Defaults` (Activity) | All four defaults correct | covered via derived |
| `BatchExportProcessorOptions<LogRecord>` (via derived class) carries correct defaults | `BatchExportProcessorOptions_Defaults` (LogRecord) | All four defaults correct | covered via derived |
| `BatchExportProcessorOptions<T>` with a third `T` (e.g. a test type) carries the same defaults | - | Constants are `T`-independent; defaults are the same | missing (low value; structural fact) |

### 2.4 Consumer-side validation surface (BatchExportProcessor\<T\>)

The `Guard.ThrowIfOutOfRange` calls in `BatchExportProcessor<T>` are the
only validation applied to the four properties today. They fire at processor
construction, not at options-assignment time. This surface is shared across
both derived-class consumers; it is documented here as the base-class
concern that both sibling files cross-reference.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `maxQueueSize < 1` -> `ArgumentOutOfRangeException` at processor ctor | - (recommended in Activity sibling Section 3.3) | `Guard.ThrowIfOutOfRange(maxQueueSize, min: 1)` at `BatchExportProcessor.cs:46` | missing |
| `maxExportBatchSize < 1` -> `ArgumentOutOfRangeException` at processor ctor | - (recommended in Activity sibling Section 3.3) | `Guard.ThrowIfOutOfRange(maxExportBatchSize, min: 1, ...)` at line 47 | missing |
| `maxExportBatchSize > maxQueueSize` -> `ArgumentOutOfRangeException` at processor ctor | - (recommended in Activity sibling Section 3.3) | Same guard at line 47 | missing |
| `scheduledDelayMilliseconds < 1` -> `ArgumentOutOfRangeException` at processor ctor | - (recommended in Activity sibling Section 3.3) | `Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1)` at line 48 | missing |
| `exporterTimeoutMilliseconds = 0` is explicitly accepted at processor ctor (`min: 0`) | - (recommended in Activity sibling Section 3.3) | `Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0)` at line 49 | missing |
| `exporterTimeoutMilliseconds < 0` -> `ArgumentOutOfRangeException` at processor ctor | - | Same guard at line 49 | missing |

These scenarios are also identified as missing in the two sibling files.
Tests may be written against either concrete derived class; they effectively
exercise the base-class guard because `BatchExportProcessor<T>` protected
ctor is the shared path.

### 2.5 No named-options or reload scenarios at base-class level

`BatchExportProcessorOptions<T>` is never registered with named-options or
with an `IOptionsMonitor`. Those scenarios are fully owned by the derived
classes and documented in the sibling files.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Named-options semantics | See `batch-export-activity-processor-options.md` Section 2.4 and `batch-export-logrecord-processor-options.md` Section 2.4 | N/A at base-class level | not applicable |
| Reload no-op baseline | See both sibling files Sections 2.6 | Not tested | covered via derived (gaps recorded in sibling files) |

---

## 3. Recommendations

This file makes a small set of recommendations that target the base-class
surface specifically. The substantive recommendations for gaps in env-var
reads, priority ordering, DI defaults, and reload behaviour are in the two
sibling files and are not duplicated here.

### 3.1 Consumer-side guard tests (shared validation surface)

The `Guard.ThrowIfOutOfRange` calls in `BatchExportProcessor<T>` are the
sole validation boundary today for all four properties. The guard tests
should live in the derived-class test files (one set is sufficient if
duplicating them adds no value; alternatively one set per derived class
makes each file self-contained). The guard scenarios are already recommended
in `batch-export-activity-processor-options.md` Section 3.3. This file
records them as base-class concerns so maintainers understand they are not
Activity-specific.

1. **`BatchExportProcessor_Guard_MaxQueueSizeLessThanOne_Throws`** (new;
   `test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`
   or a new shared processor-consumer test file).
   - Tier 1. Mechanism: Exception assertion. Construct a
     `BatchActivityExportProcessor` (concrete subclass) with
     `maxQueueSize: 0`; assert `ArgumentOutOfRangeException`.
   - Guards Issue 1. Pins `BatchExportProcessor.cs:46` as the current
     validation boundary.
   - Code-comment hint: "BASELINE: pins current deferred guard at
     processor construction. Expected to change under Issue 1
     (`IValidateOptions<T>` + `ValidateOnStart`)."
   - Risk vs reward: trivial effort; pins the guard boundary for
     both derived-class consumers.

2. **`BatchExportProcessor_Guard_ExporterTimeoutZero_IsAccepted`** (new;
   same location).
   - Tier 1. Mechanism: no exception. Construct with
     `exporterTimeoutMilliseconds: 0`; assert construction succeeds.
   - Guards Issue 1. Pins the `min: 0` rule at `BatchExportProcessor.cs:49`
     explicitly so Issue 1's validator makes a conscious decision about
     whether `0` should be accepted at the options layer as well.
   - Risk vs reward: trivial effort; low brittleness; high value for
     making the `min: 0` special case visible.

3. The remaining guard boundaries (`maxExportBatchSize < 1`,
   `maxExportBatchSize > maxQueueSize`, `scheduledDelayMilliseconds < 1`,
   `exporterTimeoutMilliseconds < 0`) are covered by recommendations in
   `batch-export-activity-processor-options.md` Section 3.3 and do not
   need to be repeated here.

### 3.2 Type-parameter-independence note (informational; no test required)

The four property defaults are `internal const int` values on
`BatchExportProcessor<T>`. Because C# `const` fields are compile-time
constants with a fixed value, not per-type-parameter values, the defaults
cannot differ between `BatchExportProcessorOptions<Activity>` and
`BatchExportProcessorOptions<LogRecord>`. No test is needed to assert this;
it is a structural fact. Maintainers may note this if a future generic
specialisation is contemplated.

---

## Guards issues

This file identifies base-class tests that guard the following entry in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. The guard scenarios in Section 2.4 (consumer-side
  `Guard.ThrowIfOutOfRange` boundaries) and the two tests recommended in
  Section 3.1 are the base-class contribution. The full set of guard tests
  for this class are distributed across this file and the two sibling files.

Reciprocal "Baseline tests required" lines should be added to Issue 1 in
`configuration-proposed-issues.md`, citing this file. Those edits happen in
the final cross-reference sweep.
