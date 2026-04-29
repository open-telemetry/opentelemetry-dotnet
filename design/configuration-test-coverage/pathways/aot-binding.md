# AOT Binding - Configuration Test Coverage

Per-pathway file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- `test/OpenTelemetry.AotCompatibility.TestApp/Program.cs` - AOT test app entry point
- `test/OpenTelemetry.AotCompatibility.TestApp/OpenTelemetry.AotCompatibility.TestApp.csproj` - TrimmerRootAssembly list
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:120-163` - reflection calls (Issue 4)
- `design/configuration-analysis.md` (Section 3.4, Section 8 point 2, Section 4.1 point 10) - AOT analysis summary
- `test/OpenTelemetry.Tests/Logs/OpenTelemetryLoggingExtensionsTests.cs`
  (`TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions`)

**AOT test app architecture:** `Program.cs` contains a single `Console.WriteLine("Passed.")` statement.
The app exists solely to act as a TrimmerRootAssembly host: at `dotnet publish` time the IL trimmer
analyses all assemblies listed under `<TrimmerRootAssembly>` and emits IL2026/IL3050 diagnostics for
any code paths that use reflection-based binding without suppression. The app does not exercise any OTel
code paths at runtime. The "test" is whether the AOT publish succeeds with an expected (or zero) diagnostic
count.

**TrimmerRootAssembly list (in-scope packages):**

- `OpenTelemetry`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`

All three in-scope packages (Section 1.1 of the entry doc) are included.

**Known Issue 4 bug - `OtlpExporterBuilder.cs:153-162`:**

`BindConfigurationToOptions` (called only when `UseOtlpExporter(IConfiguration)` is invoked) registers
four `services.Configure<T>(name, configuration)` calls. Each resolves to
`OptionsServiceCollectionExtensions.Configure<TOptions>(IServiceCollection, string, IConfiguration)`,
which internally invokes `ConfigurationBinder.Bind()`. `ConfigurationBinder.Bind()` uses reflection
to enumerate and set properties, generating IL2026 (`RequiresUnreferencedCode`) and IL3050
(`RequiresDynamicCode`) warnings in an AOT-published app.

The four call sites with their exact locations:

<!-- markdownlint-disable MD013 -->
| Call | Type bound | File:line |
| --- | --- | --- |
| `services.Configure<OtlpExporterBuilderOptions>(name, configuration)` | `OtlpExporterBuilderOptions` | `OtlpExporterBuilder.cs:153` |
| `services.Configure<LogRecordExportProcessorOptions>(name, configuration.GetSection(...))` | `LogRecordExportProcessorOptions` | `OtlpExporterBuilder.cs:155-156` |
| `services.Configure<MetricReaderOptions>(name, configuration.GetSection(...))` | `MetricReaderOptions` | `OtlpExporterBuilder.cs:158-159` |
| `services.Configure<ActivityExportProcessorOptions>(name, configuration.GetSection(...))` | `ActivityExportProcessorOptions` | `OtlpExporterBuilder.cs:161-162` |
<!-- markdownlint-enable MD013 -->

None of these calls carries `[UnconditionalSuppressMessage]`. The AOT test app includes the OTLP exporter
assembly as a TrimmerRootAssembly so the trimmer will emit diagnostics for these call sites today.

**AOT-safe alternative (for reference; implementation is out of scope for this planning cycle):**
Replace each `services.Configure<T>(name, configuration)` with an explicit options constructor that reads
individual keys via `configuration[key]` - the pattern already used by the 14 in-scope options classes.
The fix is tracked as Issue 4 in `configuration-proposed-issues.md`.

## 1. Existing coverage

<!-- markdownlint-disable MD013 -->
| File:method | Scenario summary | Observation mechanism | Env-var isolation status |
| --- | --- | --- | --- |
| `OpenTelemetryLoggingExtensionsTests.TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` | Asserts all `OpenTelemetryLoggerOptions` properties are primitive types (AOT safe) | Direct property type inspection | No env var |
| AOT test app (`dotnet publish` build step) | Trimmer analysis of all TrimmerRootAssembly assemblies; IL2026/IL3050 diagnostics surfaced at publish | AOT publish exit code + diagnostic output | n/a (publish-time check) |
<!-- markdownlint-enable MD013 -->

**Coverage gap context:** `TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` exercises exactly one of
the 14 in-scope options classes. The AOT test app's `Program.cs` is empty of OTel calls, so no options
class constructor or configuration binding method is exercised at runtime.

## 2. Scenario checklist and gap analysis

### 2.1 AOT test app runtime coverage

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Any OTel configuration path exercised at runtime in the AOT app | None - `Program.cs` contains only `Console.WriteLine("Passed.")` | App prints "Passed." and exits | Missing |
| `OtlpExporterOptions` ctor called in AOT app | None | Not exercised | Missing |
| `SdkLimitOptions` ctor called in AOT app | None | Not exercised | Missing |
| `BatchExportActivityProcessorOptions` ctor called in AOT app | None | Not exercised | Missing |
| `BatchExportLogRecordProcessorOptions` ctor called in AOT app | None | Not exercised | Missing |
| `PeriodicExportingMetricReaderOptions` ctor called in AOT app | None | Not exercised | Missing |
| `OpenTelemetryLoggerOptions` primitives check | `TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` | All properties asserted primitive | Covered (1 of 14 classes) |
| `ExperimentalOptions` ctor called in AOT app | None | Not exercised | Missing |
| `OtlpExporterBuilderOptions` ctor called in AOT app | None | Not exercised | Missing |
<!-- markdownlint-enable MD013 -->

### 2.2 Issue 4 bug: reflection-based binding in OtlpExporterBuilder

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `UseOtlpExporter(IConfiguration)` path triggers `BindConfigurationToOptions` | None (AOT app does not call this path) | IL2026/IL3050 warnings emitted at AOT publish; no runtime AOT-safety regression test | Missing |
| New reflection binding introduced in any options class ctor is caught before merge | None - only publish-time trimmer analysis catches this | IL2026/IL3050 warning at `dotnet publish`; no failing unit test | Missing |
<!-- markdownlint-enable MD013 -->

### 2.3 Trimming correctness tests per options class

<!-- markdownlint-disable MD013 -->
| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OpenTelemetryLoggerOptions` all properties are primitive | `TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` | Passes | Covered |
| Same check for remaining 13 in-scope options classes | None | Not checked | Missing |
<!-- markdownlint-enable MD013 -->

## 3. Recommendations

### 3.1 AOT test app runtime scaffold

**Target:** Add minimal OTel SDK usage to `Program.cs` so that the trimmer analyses the code paths
that are actually exercised at runtime, not just the static call graph.

**What to add (shape only; no code written in this cycle):** Each of the 14 in-scope options classes
should have its env-var-backed constructor called (or its DI registration exercised) inside `Program.cs`.
The simplest form is:

- Build a `TracerProvider`, `MeterProvider`, and `LoggerProvider` via `Sdk.Create*ProviderBuilder()`.
- Use `AddOtlpExporter()` and `UseOtlpExporter()` so that `OtlpExporterBuilder` and its
  `BindConfigurationToOptions` are reachable by the trimmer along the runtime path.

**Location:** `test/OpenTelemetry.AotCompatibility.TestApp/Program.cs`
**Tier:** n/a (AOT publish check, not a unit test)

```csharp
// BASELINE: today Program.cs is empty of OTel calls; trimmer analysis is static-graph only.
// Expected to change under Issue #4 (AOT bug fix in OtlpExporterBuilder).
// Observation: AOT publish diagnostic count - no unit-test observation mechanism.
// Coverage index: pathway.aot-binding.test-app.runtime-scaffold
```

**Risk vs reward:** High value; currently the only runtime AOT safety signal is
`TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions` for one class. Low risk of regressions from
adding calls to `Program.cs` because the app is isolated and any trimmer warning becomes a CI failure.

### 3.2 `OtlpExporterBuilder_UseOtlpExporterWithConfiguration_EmitsIL2026Warning`

**Target test name:** `OtlpExporterBuilder_UseOtlpExporterWithConfiguration_EmitsIL2026Warning`
**What this test pins:** The ABSENCE of `[UnconditionalSuppressMessage]` on the four
`services.Configure<T>(name, configuration)` calls today. A test that runs the AOT publish step
and asserts the diagnostic count or specific warning codes for `OtlpExporterBuilder.cs:153-162`.
This test is expected to change to assert ZERO warnings once Issue 4 is fixed.
**Location:** CI AOT publish step or a new `[Fact]` in a publish-time test harness (maintainer decision).
**Tier:** 3 (publish-time; not a standard unit test)
**Observation mechanism:** AOT publish diagnostic output.
**Guards issues:** Issue 4 directly.

```csharp
// BASELINE: today four IL2026/IL3050 warnings are expected at OtlpExporterBuilder.cs:153-162.
// Expected to change under Issue #4 (OtlpExporterBuilder AOT fix - replace Configure<T>(IConfiguration)
// with key-read pattern in options constructors).
// Observation: AOT publish diagnostic output - brittleness increases if IL warning message format changes.
// Coverage index: pathway.aot-binding.otlp-exporter-builder-bug
```

### 3.3 Trimming-correctness tests for remaining 13 options classes

**Target test name pattern:** `TrimmingCorrectnessOf<OptionsClass>` (13 new tests modelled on
`TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions`)
**Location:** Each in the test project for the options class's package (see Section 1.2 of entry doc).
**Tier:** 1
**Observation mechanism:** Direct property type inspection - assert each property's `PropertyType` is a
primitive, `string`, or `enum`; no complex types that would require reflection-based binding.
**Guards issues:** Issue 4 (indirect guard: a new complex property added to any options class during the
AOT fix should fail this test if it is not AOT-safe).

```csharp
// BASELINE: pins that all properties of <OptionsClass> are primitive/string/enum.
// Expected to change under Issue #4 if new properties are added.
// Observation: DirectProperty (PropertyType inspection) - low brittleness.
// Coverage index: pathway.aot-binding.<options-class-kebab>.trimming-correctness
```

**Risk vs reward:** Low effort per class (clone and adapt the existing test). Prevents silent introduction
of reflection-dependent properties during the Issue 4 refactor or the declarative-config work.

## Guards issues

- Issue 4 (AOT bug fix - replace `services.Configure<T>(IConfiguration)` with key-read pattern in
  `OtlpExporterBuilder.cs`): **direct guard** - Recommendation 3.2 pins the current diagnostic count;
  once Issue 4 lands the test asserts zero warnings. Recommendation 3.1 ensures the fixed path is
  exercised at runtime, not only by static trimmer analysis.
