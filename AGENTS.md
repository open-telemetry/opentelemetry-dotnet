# Agent Instructions for opentelemetry-dotnet

## Build, Test, and Lint

**Build:**

```sh
dotnet build OpenTelemetry.slnx --configuration Release
```

**Test all:**

```sh
dotnet test
```

**Test a single project:**

```sh
dotnet test test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj
```

**Test a single test by name:**

```sh
dotnet test test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj --filter "FullyQualifiedName~MyTestName"
```

**Test for a specific TFM** (Windows also supports `net462`):

```sh
dotnet test --framework net10.0
```

**Check code formatting:**

```sh
dotnet format OpenTelemetry.slnx --no-restore --verify-no-changes
```

**Apply code formatting:**

```sh
dotnet format OpenTelemetry.slnx
```

**Lint Markdown** (requires `markdownlint-cli`):

```sh
markdownlint .
```

- The .NET SDK version specified in `global.json` (or newer) is required.
- `TreatWarningsAsErrors` is active in Release builds; StyleCop and nullable
  violations fail the build.
- Tests run **serially** by default
  (`build/xunit.runner.json`: `maxParallelThreads: 1`).
- `net462` tests only run on Windows.

---

## Architecture

The codebase is structured around three OpenTelemetry signals (Traces, Metrics,
Logs) with a strict three-layer design:

```text
OpenTelemetry.Api          - no-op API layer (no SDK dependency)
OpenTelemetry              - SDK: provider implementations, processors, samplers
OpenTelemetry.Exporter.*   - signal-specific export backends
OpenTelemetry.Extensions.* - DI/hosting integration, propagators
```

**API layer** (`src/OpenTelemetry.Api`) - Defines the programming model
(`TracerProvider`, `MeterProvider`, `TelemetrySpan`, `Baggage`, etc.) with no-op
defaults. Libraries instrument against this layer only.

**SDK layer** (`src/OpenTelemetry`) - Implements the provider pipeline:
`TracerProviderSdk`, `MeterProviderSdk`, `LoggerProviderSdk`. Each signal has its
own `Trace/`, `Metrics/`, `Logs/` subdirectory. Contains base classes
`BaseExporter<T>`, `BaseProcessor<T>`, `Sampler`, and `Resource`.

**Exporters** - Each exporter project targets one or more signals. They extend
`BaseExporter<T>` where `T` is `Activity`, `Metric`, or `LogRecord`.

**Shared code** (`src/Shared`) - Utility files (e.g., `SemanticConventions.cs`,
`ActivityHelperExtensions.cs`) that are **linked** (not referenced) into consuming
projects via `<Compile Include="..." Link="Includes/..." />` in `.csproj` files.
Do not add `Shared/` to a project reference; use the link pattern.

**Provider builder pattern** - All providers are configured fluently:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("MyLibrary")
    .SetSampler(new AlwaysOnSampler())
    .AddConsoleExporter()
    .Build();

// Or via Microsoft.Extensions.Hosting:
services.AddOpenTelemetry()
    .WithTracing(b => b.AddConsoleExporter())
    .WithMetrics(b => b.AddConsoleExporter());
```

---

## Key Conventions

### Packages and versioning

- Versioning is done via **MinVer** (git-tag based). Tags follow the pattern
  `core-{version}` or `coreunstable-{version}`.
- **Central package management** is enabled (`Directory.Packages.props`). Never
  specify `Version=` on a `<PackageReference>` inside a project file; version
  belongs only in `Directory.Packages.props`.
- Match `Microsoft.Extensions.*` package major version to the target .NET major
  version (see the version conditions in `Directory.Packages.props`).
- The current latest stable version constant is `OTelLatestStableVer` in `Directory.Packages.props`.

### Target frameworks

- Production libraries use `$(TargetFrameworksForLibraries)` - All supported versions
  of .NET plus `netstandard2.0` and `net462`.
- Tests use `$(TargetFrameworksForTests)` = All supported versions
  of .NET plus `net462` on Windows.
- Set these via the shared MSBuild properties rather than hardcoding in `.csproj`.

### Experimental features

- Experimental public API is gated by the `EXPOSE_EXPERIMENTAL_FEATURES` compiler
  constant, which is set when `ExposeExperimentalFeatures=true` (the default for
  pre-release builds).
- Experimental APIs carry `[Experimental("OTEL####")]` and are tracked in `.publicApi/Experimental/PublicAPI.Unshipped.txt`.
- `[Obsolete]` warnings for experimental APIs (`OTEL1000`-`OTEL1004`) are suppressed
  project-wide.

### Public API tracking (`PublicApiAnalyzers`)

Every production project has a `.publicApi/` folder.

**When you add or change a public API:**

1. Use the IDE IntelliSense "Fix" to update `PublicAPI.Unshipped.txt`
   (do **not** edit manually).
2. If APIs differ per framework, place the shared portion in root files and
   overrides in per-framework subdirectories (e.g., `.publicApi/net462/`).
3. **Never modify `PublicAPI.Shipped.txt`** - only maintainers do this during releases.
4. For experimental APIs use `.publicApi/Stable/` and `.publicApi/Experimental/`
   subdirectories.

### Implementing an exporter

Extend `BaseExporter<T>` and override `Export`:

```csharp
public sealed class MyExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch) { /* ... */ }
        return ExportResult.Success;
    }
}
```

Register via an `Add*` extension method on `TracerProviderBuilder` /
`MeterProviderBuilder` / `LoggerProviderBuilder`. Follow the
`ConsoleExporterHelperExtensions.cs` pattern.

### Implementing a sampler

Extend `Sampler` and override `ShouldSample`:

```csharp
public sealed class MySampler : Sampler
{
    public MySampler() => this.Description = nameof(MySampler);

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        => new SamplingResult(SamplingDecision.RecordAndSample);
}
```

### Implementing a processor

Extend `BaseProcessor<T>` and override `OnStart`/`OnEnd`. For processors that
wrap an exporter, use `SimpleExportProcessor<T>` or `BatchExportProcessor<T>`
instead of writing your own batching logic.

### ActivitySource usage

Instrumentation libraries create a static `ActivitySource` and check for null
before using the returned `Activity`:

```csharp
private static readonly ActivitySource MySource = new("MyLibrary", "1.0.0");

using var activity = MySource.StartActivity("OperationName");
activity?.SetTag("key", "value");
```

### Code style

- `LangVersion` is set to `latest`; use current C# features freely.
- Nullable reference types are **mandatory** - never disable `#nullable`.
- `ImplicitUsings` is enabled globally.
- All source files must start with the Apache-2.0 SPDX header:

  ```csharp
  // Copyright The OpenTelemetry Authors
  // SPDX-License-Identifier: Apache-2.0
  ```

- StyleCop enforces ordering and documentation; XML doc comments are required on
  all public members (`GenerateDocumentationFile=true`).

### New projects checklist

- Use `$(TargetFrameworksForLibraries)` (or the appropriate shared property) as
  `<TargetFrameworks>`.
- Import `build/Common.prod.props` implicitly via `Directory.Build.props` - do
  **not** override `Nullable`, `LangVersion`, `EnforceCodeStyleInBuild`, or `ImplicitUsings`.
- Add `.publicApi/PublicAPI.Shipped.txt` (empty) and `.publicApi/PublicAPI.Unshipped.txt`.
- Add `<MinVerTagPrefix>` matching the component category (`core-` or `coreunstable-`).
- Add an `<InternalsVisibleTo>` entry in the source project `.csproj` for the
  companion test project.
- Shared utilities from `src/Shared/` must be included via
  `<Compile ... Link="..." />`, not as project references.

### Test conventions

- Tests live in `test/OpenTelemetry.<Component>.Tests/` mirroring the source structure.
- Reusable test helpers (mock exporters, processors, samplers) live in
  `test/OpenTelemetry.Tests/Shared/` and are linked into other test projects.
- Use `InMemoryExporter<T>` for verifying telemetry output in unit tests.
- Build the provider inside the test, not in a shared constructor/fixture, to keep
  tests isolated.
