# OpenTelemetry .NET SDK - Configuration Test Coverage

**Date:** 2026-04-23 **Author:** Steve Gordon (with AI-assisted research)
**Driver:**
[open-telemetry/opentelemetry-dotnet#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)

**Status:** Planning. No test code is written in this cycle; every output is
markdown or JSON.

---

## 0. How to read this document

This is the entry point for the configuration test safety-net planning
effort. It establishes conventions and option analyses once, so each
downstream file (per options class or per pathway) can stay tight and focus
on inventory -> gaps -> recommendations for a single surface.

Companion documents:

- [Configuration Analysis](configuration-analysis.md) - the audit this test
  work exists to protect. Section 6 points back to this tree.
- [Deep Dives](configuration-analysis-deep-dives.md) - subsystem detail.
  Referenced by anchor from downstream files; do not load in full.
- [Risk Register](configuration-analysis-risks.md) - risks this test net
  pins. Includes a "Test strategy" subsection on how the safety net
  mitigates env-var globalness, reflection brittleness, and related risks.
- [Proposed Issues](configuration-proposed-issues.md) - 26 sub-issues each
  cross-referenced with "Baseline tests required" lines to files in this
  tree.
- [`configuration-test-coverage/existing-tests.md`](configuration-test-coverage/existing-tests.md)
  - Session 0a inventory of current config-adjacent tests. The
  authoritative source of facts about what tests exist today. Every file in
  the per-class and per-pathway trees references it rather than
  re-deriving.

The file tree under `configuration-test-coverage/` is split into:

- `options/` - one file per in-scope options class.
- `pathways/` - one file per cross-cutting pathway that is not owned by a
  single options class.

Each file has the same three sections: existing coverage, scenario
checklist and gap analysis, recommendations. Ordering is identical across
files so a maintainer can scan any file and trust the structure.

Session working rule: to continue any downstream file, load this entry
doc, the relevant rows of `existing-tests.md`, and the one `src/` file for
the class or pathway in question. Do not load the full analysis set.

---

## 1. In-scope packages and options-class inventory

### 1.1 Packages in scope

| Package | Test project |
| --- | --- |
| `OpenTelemetry` | `test/OpenTelemetry.Tests/` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/` |
| `OpenTelemetry.Extensions.Hosting` | `test/OpenTelemetry.Extensions.Hosting.Tests/` |

Out of scope for this pass: Console, Zipkin, Prometheus exporters;
`OpenTelemetry.Shims.*`; `OpenTelemetry.Extensions.Propagators`. These are
flagged for a later cycle.

### 1.2 Options classes in scope

14 options classes mapped to source locations. The comprehensive table of
constructor parameters, env-var mappings, DI registrations, and reload
candidacy lives in [Deep Dive
A.0](configuration-analysis-deep-dives.md#a0-summary-tables); this table
is the index downstream files use to open the right source file.

The `Existing tests` column cites test counts derived from the Session 0a
inventory - one line per file from
[`existing-tests.md`](configuration-test-coverage/existing-tests.md). Counts
are indicative. No quality classification is made here; that is per-file
work.

| # | Options class | `src/` file:line | Visibility | Existing tests | Coverage doc |
| --- | --- | --- | --- | --- | --- |
| 1 | `OtlpExporterOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:25` | public | ~22 in `OtlpExporterOptionsTests.cs`, plus named-options tests across signal-specific exporter tests | [otlp-exporter-options.md](configuration-test-coverage/options/otlp-exporter-options.md) |
| 2 | `OtlpExporterBuilderOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:13` | internal | ~14 in `UseOtlpExporterExtensionTests.cs` | [otlp-exporter-builder-options.md](configuration-test-coverage/options/otlp-exporter-builder-options.md) |
| 3 | `OtlpTlsOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:17` | internal | ~12 in `OtlpTlsOptionsTests.cs` (plus helper-class tests) | [otlp-tls-options.md](configuration-test-coverage/options/otlp-tls-options.md) |
| 4 | `OtlpMtlsOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:17` | internal | 7 in `OtlpMtlsOptionsTests.cs`, ~13 in `OtlpSecureHttpClientFactoryTests.cs` | [otlp-mtls-options.md](configuration-test-coverage/options/otlp-mtls-options.md) |
| 5 | `SdkLimitOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs:8` | internal | 5 in `SdkLimitOptionsTests.cs` | [sdk-limit-options.md](configuration-test-coverage/options/sdk-limit-options.md) |
| 6 | `ExperimentalOptions` | `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs:8` | internal | 1 usage in `OtlpExporterOptionsExtensionsTests.GetTransmissionHandler_*` | [experimental-options.md](configuration-test-coverage/options/experimental-options.md) |
| 7 | `BatchExportActivityProcessorOptions` | `src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs:15` | public | 6 in `Trace/BatchExportActivityProcessorOptionsTests.cs` | [batch-export-activity-processor-options.md](configuration-test-coverage/options/batch-export-activity-processor-options.md) |
| 8 | `BatchExportLogRecordProcessorOptions` | `src/OpenTelemetry/Logs/Processor/BatchExportLogRecordProcessorOptions.cs:15` | public | 5 in `Logs/BatchExportLogRecordProcessorOptionsTests.cs` | [batch-export-logrecord-processor-options.md](configuration-test-coverage/options/batch-export-logrecord-processor-options.md) |
| 9 | `BatchExportProcessorOptions<T>` | `src/OpenTelemetry/BatchExportProcessorOptions.cs:10` | public (abstract base) | tested indirectly via (7) and (8) | [batch-export-processor-options.md](configuration-test-coverage/options/batch-export-processor-options.md) |
| 10 | `PeriodicExportingMetricReaderOptions` | `src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs:16` | public | 9 in `Internal/PeriodicExportingMetricReaderHelperTests.cs` plus temporality theories in OTLP metrics tests | [periodic-exporting-metric-reader-options.md](configuration-test-coverage/options/periodic-exporting-metric-reader-options.md) |
| 11 | `MetricReaderOptions` | `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs:12` | public | resolved in `UseOtlpExporterConfigurationTest` theories | [metric-reader-options.md](configuration-test-coverage/options/metric-reader-options.md) |
| 12 | `OpenTelemetryLoggerOptions` | `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs:13` | public | 7 in `Logs/OpenTelemetryLoggingExtensionsTests.cs` | [opentelemetry-logger-options.md](configuration-test-coverage/options/opentelemetry-logger-options.md) |
| 13 | `LogRecordExportProcessorOptions` | `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs:11` | public | resolved in `UseOtlpExporterConfigurationTest` theories | [log-record-export-processor-options.md](configuration-test-coverage/options/log-record-export-processor-options.md) |
| 14 | `ActivityExportProcessorOptions` | `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs:12` | public | resolved in `UseOtlpExporterConfigurationTest` theories | [activity-export-processor-options.md](configuration-test-coverage/options/activity-export-processor-options.md) |

Shared infrastructure referenced by most classes:

- `DelegatingOptionsFactory<T>` - `src/Shared/Options/DelegatingOptionsFactory.cs:29`
- `SingletonOptionsManager<T>` - `src/Shared/Options/SingletonOptionsManager.cs:11`

Downstream files cite property declarations, env-var reads, and consumer
sites at `file:line` precision using these entry points.

---

## 2. Testing modalities and observation mechanisms

Neutral catalogue. Each per-class and per-pathway file selects per
scenario and justifies. The decision-support rule is: pick the
lowest-effort mechanism whose brittleness risk is acceptable for the
scenario. There is no globally preferred mechanism.

### 2.1 Direct property on a constructed options instance

Construct the options type directly (or via a factory) and assert on public
properties.

- **Pros.** No DI surface, no reflection, trivial to write and read.
- **Cons.** Cannot observe behaviour applied during DI build; cannot
  observe `Configure<T>` / `PostConfigure<T>` / `IValidateOptions<T>`
  interactions.
- **Effort.** Low.
- **Brittleness.** Low.
- **Codebase example.** Most of `OtlpExporterOptionsTests.cs` (Session 0a
  Sec.3.A).

### 2.2 DI resolution of `IOptions<T>` / `IOptionsMonitor<T>` / `IOptionsSnapshot<T>`

Build an `IServiceCollection`, add OTel, resolve the options interface from
the resulting `IServiceProvider`.

- **Pros.** Exercises the real DI pipeline
  (`DelegatingOptionsFactory` -> `Configure<T>` -> `PostConfigure<T>` ->
  validation). Works with named options. No production seam.
- **Cons.** More setup; slower; harder to isolate against ambient env vars.
- **Effort.** Low to medium.
- **Brittleness.** Low for options-only assertions; increases if asserting
  on built pipeline state.
- **Codebase example.**
  `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` resolves
  `IOptionsMonitor<OtlpExporterBuilderOptions>` and calls
  `.Get(name)` for named-options probes (Session 0a Sec.3.B).

### 2.3 Reflection on private fields of built components

After building a provider/processor/exporter, reflect into private fields
to observe the baked-in values.

- **Pros.** Observes "what the component actually runs with" after
  construction when no public accessor exists.
- **Cons.** Brittle under internal renames. Ties tests to implementation
  detail. Duplicates what `InternalsVisibleTo` plus a named internal
  accessor can express at lower cost.
- **Effort.** Low per test, high in aggregate (review cost, churn).
- **Brittleness.** High.
- **Codebase example.**
  `OtlpMetricsExporterTests.cs:55` uses `BindingFlags.NonPublic` to access
  `MeterProviderSdk.Reader` (Session 0a Sec.3.C). No existing test
  reflects on a private *options* field; when downstream files propose
  this mechanism, they must name the target field (e.g.
  `BatchExportProcessor<T>.scheduledDelayMilliseconds`,
  `OtlpExporterTransmissionHandler.TimeoutMilliseconds`).

### 2.4 `InternalsVisibleTo` with an internal accessor

Surface an internal property or method (already accessible to tests) that
exposes the configured value without reflection.

- **Pros.** Compile-checked. No reflection. Rename-safe via refactor
  tools.
- **Cons.** Requires an internal member in `src/` that tests can read. If
  it does not exist, the seam is a production change, which is out of
  scope for this planning cycle (see non-goals). If the property *already
  exists internally* this is the preferred path.
- **Effort.** Low when the seam already exists.
- **Brittleness.** Low.
- **Codebase example.** `OpenTelemetryServicesExtensionsTests.*_DisposalTest`
  casts the resolved provider to the internal
  `TracerProviderSdk`/`MeterProviderSdk`/`LoggerProviderSdk` type and
  reads `Disposed`/`OwnedServiceProvider` (Session 0a Sec.3.C). The
  `InternalsVisibleTo` wiring is documented in Session 0a Sec.4.G.

### 2.5 Behavioural observation via mock exporter or test sampler

Wire a fake processor/exporter/sampler into the pipeline and assert on
what it received.

- **Pros.** No reflection. Observes the *effect* of an options value
  rather than the value itself - closer to user-visible behaviour.
- **Cons.** Requires building a fake component and constructing real
  telemetry. Harder to attribute a failure to a specific options
  property.
- **Effort.** Medium.
- **Brittleness.** Low (behaviour-level contracts are stable).
- **Codebase example.**
  `UseOtlpExporterExtensionTests.UseOtlpExporterAddsTracingProcessorToPipelineEndTest`
  uses a local `TestLogRecordProcessor`-style stand-in (Session 0a
  Sec.3.D). `DelegatingExporter.cs` / `DelegatingProcessor.cs` are shared
  helpers already used across the core tests.

### 2.6 Wire-level observation via in-process listener or mock collector

Host an in-process HTTP or gRPC listener and assert on the outgoing
request (URL, headers, TLS certificate, protobuf payload).

- **Pros.** The only way to observe properties that are invisible
  in-process (e.g. exporter `Endpoint`, `Headers`, `Protocol`, TLS cert
  loaded).
- **Cons.** Heaviest setup. Network plumbing. Slowest to run. Potential
  for port contention in CI.
- **Effort.** High.
- **Brittleness.** Low at contract level; high if asserting on transport
  implementation detail.
- **Codebase example.** `MockCollectorIntegrationTests.cs` hosts an
  ASP.NET Core HTTP/gRPC mock collector; `TestHttpServer.cs` provides a
  lightweight HTTP listener for single-request assertions (Session 0a
  Sec.3.E and Sec.4.E).

### 2.7 EventSource observation

Attach an `InMemoryEventListener` / `EventSourceTestHelper`-based listener
and assert on emitted events.

- **Pros.** Pins today's (silent-failure) observability surface so Issue
  6 / Risk 4.7 changes produce a visible delta. Already has shared
  infrastructure.
- **Cons.** Only useful for scenarios the SDK currently logs (or is
  expected to log).
- **Effort.** Low given the existing helpers.
- **Brittleness.** Medium - event IDs and payload shapes can change.
- **Codebase example.**
  `OpenTelemetryMetricsBuilderExtensionsTests.ReloadOfMetricsViaIConfiguration*`
  uses `InMemoryEventListener` alongside `IOptionsMonitor<MetricsOptions>`
  reload (Session 0a Sec.3.F).

---

## 3. Runtime tiers

Presented as analysis. CI policy (run every PR vs opt-in vs nightly) is
left to maintainers.

| Tier | Budget per test | What it runs | What it can prove | What it cannot prove |
| --- | --- | --- | --- | --- |
| **Tier 1** - in-proc unit | < 50 ms | Direct options construction, or a minimal `IServiceCollection` | Options defaults, env-var binding via `IConfiguration`, priority between `Configure<T>` and constructor reads | End-to-end DI composition where hosting extensions matter; wire behaviour |
| **Tier 2** - in-proc DI / integration | < 500 ms | `AddOpenTelemetry(...)` through host or standalone builder; named options; `UseOtlpExporter` overloads | Named-options resolution; provider assembly ordering; composition parity across host / standalone | Behaviour depending on process-global env-var state set *before* the test runs |
| **Tier 3** - out-of-proc env-var harness | < 3 s | Child process with a prepared env manifest; readback of effective options via IPC (stdout JSON or file) | True env-var semantics without snapshot/restore risk; parallelism without collection serialisation | Nothing the lower tiers already prove (do not duplicate) |

Tier 1 and 2 are applicable to every in-scope scenario. Tier 3 is for the
specific subset where in-process snapshot/restore is insufficient - for
example, scenarios that read env vars during static initialisation, or
scenarios that deliberately exercise *absence* of a variable while other
tests in the same collection set it.

Budget numbers above are targets for authorship, not hard gates. Actual CI
gating depends on the process-isolation option selected (see Section 4)
and the env-var isolation pattern selected (Section 5).

---

## 4. Process-isolation strategy options

Presented neutrally. No preferred pick. The analysis feeds a maintainer
decision before Tier 3 test work begins.

### Option A - dedicated Tier 3 test project

A new project (e.g. `OpenTelemetry.Configuration.ProcessTests`) housing
only the child-process tests, conditionally excluded from default CI via a
trait filter or an MSBuild property.

- **Pros.** Strong separation; no risk of accidentally importing Tier 3
  helpers into fast-tier tests; easy to opt out of by trait or project
  reference.
- **Cons.** New project to maintain (csproj, CI wiring, code style
  configuration). Adds a third axis to "where does the test go?" for
  contributors. Duplicated base helpers across it and the existing three
  test projects unless a shared `test/Shared` project is also introduced.
- **Effort.** Medium. One-time bootstrap plus ongoing project overhead.
- **Maintenance cost.** Medium - a second place to keep collection
  definitions, InternalsVisibleTo, and target-framework choices in sync.
- **Discoverability.** High - a new project is easy to find and filter.

### Option B - helper library consumed from existing test projects

A shared library (e.g. `test/Shared/ProcessIsolation/`) that spawns child
processes. Tests live in the existing three projects but opt into Tier 3
by calling the helper, which runs a small launcher with an env manifest
and reads back effective options via stdout JSON or a temp file.

- **Pros.** No new test project. Tests stay next to their subject. Easy
  to mix Tier 1/2/3 assertions in one file per class if desired. Shared
  launcher keeps IPC contract in one place.
- **Cons.** Tier 3 tests share a runtime/collection with faster tests in
  the same project; if a test author forgets the Tier trait, a heavy
  test runs on the default CI leg. The launcher binary or compiled-at-
  runtime script needs a home; adds a small amount of complexity inside
  the helper.
- **Effort.** Medium. Designing the IPC contract (effective-options
  serialisation schema) is the main piece.
- **Maintenance cost.** Low to medium once IPC is stable.
- **Discoverability.** Medium - Tier 3 tests only stand out via trait.

### Option C - in-process isolation via AppDomain / AssemblyLoadContext or fixture snapshot/restore

No second process. Either load the SDK under test into an isolated
`AssemblyLoadContext` (or `AppDomain` on .NET Framework TFMs) so env-var
reads and static initialisation are per-test, or rely on the existing
`IDisposable` class-level snapshot/restore plus `[Collection]` grouping
pattern and defer Tier 3 entirely.

- **Pros.** No IPC surface. Fastest of the three. Uses infrastructure the
  codebase already has (Session 0a Sec.2.A/2.C).
- **Cons.** Env vars are still process-global; snapshot/restore cannot
  protect against parallel test collections. `AssemblyLoadContext` does
  not isolate environment variables; it isolates only managed state.
  AppDomain isolation is unavailable on .NET Core and modern .NET TFMs.
  Scenarios that fail only under true process isolation (static
  initialisers reading env vars before the test runs) remain unreachable.
- **Effort.** Low. Mostly a decision to adopt and a tightening of the
  existing pattern.
- **Maintenance cost.** Low at rest; high if a subtle flake surfaces
  because of env-var globalness.
- **Discoverability.** Low - the absence of a visible Tier 3 category
  makes it easier to overlook scenarios that would benefit from it.

A hybrid (C for the common case, B or A for a small set of scenarios) is
also feasible. Downstream pathway files call out scenarios that require
*true* process isolation so the maintainer decision has a concrete count
to weigh.

---

## 5. Env-var isolation pattern options (Tier 1 / Tier 2)

Each pattern is already represented in the codebase (Session 0a Sec.2).
Choice here informs the per-class file recommendations.

| Pattern | Pros | Cons | Effort | Codebase usage |
| --- | --- | --- | --- | --- |
| Class-level `IDisposable` snapshot/restore | Per-class isolation; constructor/`Dispose` symmetry | Constructor fan-out as more env vars are added; relies on test class never being instantiated in parallel with itself (xUnit default) | Low | `OtlpExporterOptionsTests`, `SdkLimitOptionsTests`, `BatchExportActivityProcessorOptionsTests`, many others (Session 0a Sec.2.A) |
| `using (new EnvironmentVariableScope(...))` | Scoped to a single block; co-located with the arrange step | One variable per scope -> nested `using`s accumulate; helper not linked into OTLP project today | Low | `TracerProviderIsExpectedType`, `WhenOpenTelemetrySdkIsDisabledExceptionNotThrown` (Session 0a Sec.2.B) |
| Attribute-only `[Collection("EnvVars")]` | Serialises mutating tests across files; no per-test boilerplate | Kills parallelism across the whole collection; no definition class means xUnit discovery is implicit | Very low | `OtlpExporterOptionsTests`, `OtlpMetricsExporterTests`, `UseOtlpExporterExtensionTests` (Session 0a Sec.2.C) |
| `ICollectionFixture` / `[CollectionDefinition]` with snapshot lifecycle | Single snapshot per collection run; clear lifecycle; can seed deterministic state | More machinery; none of the three projects use collection fixtures today (Session 0a Sec.4.A) | Medium | Not currently used |
| Defer to Tier 3 child process | No process-global mutation at all | All costs of Tier 3 | Medium to high | Not currently used |

A combined approach is common in practice: `IDisposable` snapshot/restore
at class level for Tier 1/2, `[Collection]` grouping to serialise
high-contention tests, Tier 3 for the residual set. The per-class files
flag their assumed pattern; the final choice is a project-wide convention
decided alongside Option A/B/C.

---

## 6. Naming and trait conventions

Locked from the Session 0a xUnit naming survey.

- **Primary style.** `Subject_Condition_Expected` or
  `Method_Condition_Expected`, underscore-separated. This is the dominant
  pattern across all three test projects (Session 0a Sec.5.A).
- **Secondary style (permitted).** `FeatureDescriptionTest` (suffix
  `Test`, no underscores) is acceptable when the scenario does not
  decompose into a three-part name and the file already uses the suffix
  convention.
- **Avoid.** `When<Condition><Outcome>` and `Test<Subject>` prefixes -
  both are in use but at low share; new tests should not add to them.
- **Theories.** Method named once; `InlineData` drives the scenarios.

Trait additions (minimal, for filterability only):

| Trait | Values | Purpose |
| --- | --- | --- |
| `Tier` | `1`, `2`, `3` | Maps to the runtime tiers in Section 3. Enables `dotnet test --filter Tier!=3` |
| `Pathway` | one of the names under `pathways/` | Lets a maintainer filter to e.g. all env-var-precedence tests |
| `GuardsIssue` | issue number from `configuration-proposed-issues.md` | Lets the reviewer of a specific issue surface the tests that break when the issue lands |

`Tier` is the only trait required for Tier 3 selection. `Pathway` and
`GuardsIssue` are strongly recommended but optional.

---

## 7. Cross-cutting pathway index

Each pathway has a file under `configuration-test-coverage/pathways/`.

| Pathway file | Focus |
| --- | --- |
| `delegating-options-factory-priority.md` | Factory -> `Configure` -> `PostConfigure` -> `Validate` ordering |
| `env-var-precedence.md` | Env var vs `appsettings.json` vs `Configure<T>` vs constructor default |
| `named-options-resolution.md` | Named-options semantics; default-name mapping |
| `reload-no-op-baseline.md` | Shared spec for "reload must not change component behaviour today" |
| `aot-binding.md` | AOT / trimming audit over config paths |
| `vendored-env-var-parity.md` | Parity between `src/Shared/EnvironmentVariables/` and the runtime package |
| `provider-global-switches.md` | `OTEL_SDK_DISABLED`, `OTEL_METRICS_EXEMPLAR_FILTER` |
| `singleton-options-manager.md` | Post-build `Configure` silent no-op on `OpenTelemetryLoggerOptions` |
| `try-add-singleton-first-wins.md` | First-writer-wins DI ordering (Risk 1.3) |
| `env-var-fallback-chains.md` | Signal-specific to generic OTLP fallback chain |
| `host-vs-standalone-parity.md` | `AddOpenTelemetry` through Hosting vs standalone builder |
| `observability-and-silent-failures.md` | Characterisation of current `OpenTelemetrySdkEventSource` output |

---

## 8. Deliverable breakdown (shape only)

The deliverable table is populated by the final summary and
cross-reference sweep once every per-class and per-pathway file has
produced its recommendations. Columns are fixed here so downstream files
can populate rows as they go.

Rows are ordered by prerequisite dependency. Items 1-3 are maintainer
decisions that gate test authorship. Items 4-14 are cross-cutting
pathway deliverables that most per-class deliverables depend on. Items
15-28 are the per-options-class baseline deliverables; those can
parallelise once the pathway deliverables they cite have landed. PR
estimates are indicative only; the maintainer decides final
bundling.

<!-- markdownlint-disable MD013 -->

| # | Deliverable | Prereqs | Depends on files | Tier mix | Estimated PRs |
| --- | --- | --- | --- | --- | --- |
| 1 | Env-var isolation pattern selection + reusable fixture | Maintainer decision across Section 5 options | all files that set `OTEL_*` env vars | n/a (infra) | 1 |
| 2 | Snapshot library selection + EventSource listener wiring in OTLP test project | Maintainer decision (Appendix A); `InMemoryEventListener` link decision | `experimental-options.md`, `observability-and-silent-failures.md`, `otlp-mtls-options.md` | n/a (infra) | 1 |
| 3 | Tier 3 process-isolation approach decision (Options A/B/C in Section 4) | Maintainer decision | scenarios flagged Tier 3 across tree | n/a (infra) | 0-1 |
| 4 | Vendored env-var parity tests (guards Issue 3) | 1 | `pathways/vendored-env-var-parity.md` | T1 | 1 |
| 5 | `DelegatingOptionsFactory` priority-order pathway tests (baseline for all options) | 1 | `pathways/delegating-options-factory-priority.md` | T2 | 1 |
| 6 | Env-var precedence matrix pathway tests | 1, 5 | `pathways/env-var-precedence.md` | T1-T2 | 1-2 |
| 7 | Reload no-op baseline shared spec and helpers | 1 | `pathways/reload-no-op-baseline.md` | T2 | 1 |
| 8 | `SingletonOptionsManager` post-build `Configure` silent no-op pathway | 1, 7 | `pathways/singleton-options-manager.md` | T2 | 1 |
| 9 | `TryAddSingleton` first-wins pathway (Risk 1.3; guards Issue 6) | 1 | `pathways/try-add-singleton-first-wins.md` | T1-T2 | 1 |
| 10 | Named-options resolution pathway | 1, 5 | `pathways/named-options-resolution.md` | T2 | 1 |
| 11 | Env-var fallback-chain pathway (signal-specific to generic OTLP) | 1, 6 | `pathways/env-var-fallback-chains.md` | T1-T2 | 1 |
| 12 | Provider-global switches (`OTEL_SDK_DISABLED`, exemplar filter) | 1 | `pathways/provider-global-switches.md` | T1-T2 | 1 |
| 13 | Observability + silent-failures EventSource characterisation (guards Issue 6, Risk 4.7) | 2 | `pathways/observability-and-silent-failures.md` | T2 | 1-2 |
| 14 | AOT binding audit + extensions (guards Issue 4) | 1 | `pathways/aot-binding.md` | T2-T3 | 1 |
| 15 | Host-vs-standalone parity suite | 1, 5, 6, 10 | `pathways/host-vs-standalone-parity.md` | T2 | 1-2 |
| 16 | Snapshot pilot: `ExperimentalOptions` (smallest surface) | 2 | `options/experimental-options.md` | T1-T2 | 1-2 |
| 17 | `SdkLimitOptions` pre/post cascade characterisation (guards Issues 5, 10) | 1, 5, 6, 7, 11 | `options/sdk-limit-options.md` | T1-T2 | 1-2 |
| 18 | `OtlpExporterOptions` baseline (endpoint, protocol, headers, timeout, named options) | 1, 5, 6, 7, 10 | `options/otlp-exporter-options.md` | T1-T2 | 3-4 |
| 19 | `OtlpExporterBuilderOptions` baseline + AOT-bug guard (guards Issue 4) | 1, 5, 7, 10, 14 | `options/otlp-exporter-builder-options.md` | T1-T2 | 3-4 |
| 20 | `OtlpTlsOptions` baseline (`#if NET`) | 1, 5 | `options/otlp-tls-options.md` | T1-T2 | 1-2 |
| 21 | `OtlpMtlsOptions` baseline + EventSource probes (`#if NET`) | 1, 2, 5, 13 | `options/otlp-mtls-options.md` | T1-T2 | 1-2 |
| 22 | `BatchExportProcessorOptions<T>` base guards | 1 | `options/batch-export-processor-options.md` | T1 | 1 |
| 23 | `BatchExportActivityProcessorOptions` baseline + reload no-op | 1, 5, 6, 7, 22 | `options/batch-export-activity-processor-options.md` | T1-T2 | 2-3 |
| 24 | `BatchExportLogRecordProcessorOptions` baseline + reload no-op | 1, 5, 6, 7, 22 | `options/batch-export-logrecord-processor-options.md` | T1-T2 | 2-3 |
| 25 | `PeriodicExportingMetricReaderOptions` baseline + reload no-op | 1, 5, 6, 7 | `options/periodic-exporting-metric-reader-options.md` | T1-T2 | 1-2 |
| 26 | `MetricReaderOptions` baseline (temporality, aggregation) | 1, 5, 7, 25 | `options/metric-reader-options.md` | T1-T2 | 1-2 |
| 27 | `LogRecordExportProcessorOptions` baseline (processor-type selection) | 1, 5, 7, 10, 22 | `options/log-record-export-processor-options.md` | T1-T2 | 1-2 |
| 28 | `ActivityExportProcessorOptions` baseline (processor-type selection) | 1, 5, 7, 22 | `options/activity-export-processor-options.md` | T1-T2 | 1-2 |
| 29 | `OpenTelemetryLoggerOptions` baseline + singleton no-op | 1, 5, 7, 8 | `options/opentelemetry-logger-options.md` | T1-T2 | 2-3 |

<!-- markdownlint-enable MD013 -->

Notes:

- Items 1-3 are decision gates, not code deliverables; maintainer sign-off
  is required before any downstream item begins authorship.
- Items 4-15 are the cross-cutting pathway deliverables (12 pathway files
  plus host-vs-standalone parity). The deliverable table references each
  pathway file exactly once so no scenario is owned twice.
- Items 16-29 are the 14 per-options-class deliverables. Item 16
  (`ExperimentalOptions`) is sequenced first among options because it
  doubles as the snapshot-library pilot (Appendix A).
- Every item above cites `pathways/reload-no-op-baseline.md` transitively
  via the "reload no-op baseline" subsection recommended in each
  per-options-class file. When item 7 lands, the per-class items adopt
  the shared spec rather than re-specifying it.

---

## 9. Adjacent findings register

Findings that the test planning uncovers but that are *not*
config-specific. Columns are fixed; rows accumulate as per-class and
per-pathway files surface them. Each row is tagged **pre-config**,
**during-config**, or **post-config** to signal sequencing against the
primary initiative.

| Finding | Detail | Timing | Source file (this tree) |
| --- | --- | --- | --- |
| ... | ... | ... | ... |

Rule: rows are surfaced here so they are not lost, but the primary
deliverable remains config. No row in this table becomes a test in the
current planning cycle.

---

## 10. Code-comment template

Every test added by this safety net that pins expected-to-change
behaviour carries the template below. Locked here.

```csharp
// BASELINE: pins current behaviour.
// Expected to change under Issue #<n> (<short description>).
// Guards risks: <Risk X.Y>[, <Risk A.B>].
// Observation: <DI|DirectProperty|InternalAccessor|Reflection|Mock|Wire|EventSource>
// - <one-line note on brittleness or scope>.
// Coverage index: <scenarioId>
```

Rules:

- `Issue #<n>` matches an entry in
  [`configuration-proposed-issues.md`](configuration-proposed-issues.md).
- `<Risk X.Y>` matches a section id in
  [`configuration-analysis-risks.md`](configuration-analysis-risks.md).
- `<scenarioId>` is the scenario identifier defined in Section 11.
- Tests that are *not* expected to change may omit the `Issue` line but
  must still cite the scenario id.

A minimal stable-baseline variant (no planned change):

```csharp
// BASELINE: pins current behaviour. No planned change.
// Observation: <mechanism> - <one-line note>.
// Coverage index: <scenarioId>
```

---

## 11. Scenario-id format

Dotted path, lowercase, hyphen-separated tokens within each segment.

```text
<options-class-or-pathway>.<property-or-scope>.<aspect>
```

Examples:

- `otlp-exporter-options.endpoint.env-var-precedence`
- `otlp-exporter-options.headers.parsing-url-encoded`
- `batch-export-activity-processor-options.scheduled-delay.default`
- `pathway.env-var-precedence.programmatic-over-appsettings`
- `pathway.aot-binding.otlp-exporter-builder-bug`

Segments:

1. The options class name in kebab-case, **or** `pathway.<name>` for
   cross-cutting files.
2. The property name in kebab-case, **or** a scope label (`defaults`,
   `ctor`, `all-properties`) when the scenario is not property-specific.
3. The aspect: `default`, `env-var`, `iconfiguration`, `env-var-precedence`,
   `named-options`, `reload-no-op`, `invalid-input`, `consumer-effect`, or
   a pathway-specific label.

Uniqueness: the full dotted path is the canonical identifier for a
scenario. Duplicates are a lint error.

---

## Appendix A - Snapshot library comparison

Constraint: free, open source, and actively maintained (a release within
the last 12 months). All four candidates are evaluated against that bar.
The 12-month cutoff is 2025-04-23. Maintainers select.

| Candidate | Latest release | Within 12 months? | Licence | Primary source |
| --- | --- | --- | --- | --- |
| Verify | 31.16.1 (package); Verify.Xunit 31.12.5, Verify.XunitV3 31.15.0 | Yes | MIT | [VerifyTests/Verify](https://github.com/VerifyTests/Verify) |
| ApprovalTests.Net | 7.0.0 (2025-07-03) | Yes | Apache-2.0 | [approvals/ApprovalTests.Net](https://github.com/approvals/ApprovalTests.Net) |
| Snapshooter | Snapshooter 1.3.1; Snapshooter.Xunit 1.1.0 (2025-12-22); Snapshooter.NUnit 1.3.1 (2026-02-18) | Yes | MIT | [SwissLife-OSS/snapshooter](https://github.com/SwissLife-OSS/snapshooter) |
| Roll-your-own | n/a | n/a | n/a | n/a |

### Verify

- **Maintenance.** Very active. Multiple releases per month; wide xUnit /
  MSTest / NUnit / xUnit v3 coverage.
- **Transitive dependencies.** Verify depends on DiffPlex and source
  generators; Verify.Xunit adds xUnit. Small.
- **IDE / diff tooling.** First-class Rider/VS integration; auto-accepts
  via file diff tool.
- **Effort to integrate.** Low for a pilot on one options class; the
  `Verifier.Verify(options)` call returns a `SettingsTask` that can be
  customised (e.g., scrub timestamps).
- **Footprint in this repo (approx).** If adopted for the 14 options
  classes, ~14 default-state snapshot tests plus per-property override
  tests. Shared infrastructure: one or two `VerifySettings` customisations
  for scrubbing and option-member sorting.
- **Trade-off.** Most feature-complete; largest surface to understand;
  newest framework for contributors unfamiliar with snapshot testing.

### ApprovalTests.Net

- **Maintenance.** 7.0.0 released 2025-07-03; NuGet listing explicitly
  notes the project is not being actively maintained and recommends
  Verify. Passes the 12-month bar on release cadence but fails the
  spirit of the "actively maintained" requirement. Maintainers should
  weigh whether to treat the NuGet note as disqualifying.
- **Transitive dependencies.** Small.
- **IDE / diff tooling.** Reporter-based diffing; works with common
  diff tools via reporters.
- **Effort to integrate.** Low.
- **Footprint.** Similar to Verify.
- **Trade-off.** Familiar to teams using it historically; discouraged by
  its own maintainers in favour of Verify.

### Snapshooter

- **Maintenance.** Recent releases in both 2025 and 2026; cadence lower
  than Verify but within scope.
- **Transitive dependencies.** Newtonsoft.Json (relevant given the SDK
  otherwise avoids Newtonsoft).
- **IDE / diff tooling.** Basic file-based snapshots; diff via editor.
- **Effort to integrate.** Low.
- **Footprint.** Similar to Verify.
- **Trade-off.** Smallest of the three managed libraries; pulling in
  Newtonsoft.Json in the test projects is a new transitive.

### Roll-your-own

- Serialise each options class to JSON (via
  `System.Text.Json` with a stable options-set) and compare against a
  checked-in file.
- **Maintenance.** Zero external; one helper plus a convention.
- **Dependencies.** None beyond `System.Text.Json` already used.
- **IDE / diff tooling.** Standard git diff; no snapshot-specific
  reviewer affordances.
- **Effort to integrate.** Low initial, higher at repeat authorship -
  the helper must handle named-options, property ordering stability,
  trimming of derived properties, and diff failure messages.
- **Footprint.** For 14 classes: ~14 snapshot files plus a shared helper
  (~100-200 LOC estimate). No library dependency; every edge case is
  handled in-repo.
- **Trade-off.** Control vs carry cost; no third-party version churn.

Selection criterion examples (maintainer weights):

- Prefer active maintenance and first-class IDE diffing -> Verify.
- Prefer zero third-party dependency in the test tree -> Roll-your-own.
- Prefer familiarity with existing ApprovalTests workflows -> ApprovalTests,
  noting the maintenance caveat.
- Minimise transitive dependencies while keeping snapshot library
  semantics -> Snapshooter, with the Newtonsoft.Json caveat.

Pilot scope recommendation (before rolling out): apply the chosen library
to one options class (candidate: `ExperimentalOptions` - small surface,
internal, few named variants) and only expand once the diff workflow is
validated in CI.

---

## Non-goals (reminder)

Carried from the master plan. No tests for not-yet-implemented env vars
or options classes; no test implementation in this cycle; no edits to
production `src/`; no preemptive reorganisation of existing tests; no
scope creep into non-config observability; no disposal or subscription-
lifetime tests by default.
