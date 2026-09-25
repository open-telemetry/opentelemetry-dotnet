---
name: performance-benchmark
description: Generate and run ad hoc performance benchmarks to validate code changes using BenchmarkDotNet and benchmark.ps1. Use when asked to benchmark, profile, or validate the performance impact of a code change in opentelemetry-dotnet.
---

# opentelemetry-dotnet Performance Benchmarking

This repository already has a `test/Benchmarks` BenchmarkDotNet project and a
root [`benchmark.ps1`](../../../benchmark.ps1) script that checks out a target
and baseline ref, builds, and runs both for you - you rarely need to hand-build
baseline/changed hosts the way lower-level runtimes do.

## Step 1: Write or Extend a Benchmark

Prefer adding a `[Benchmark]` method to an existing class under
`test/Benchmarks/<Area>/` (e.g. `Trace/`, `Metrics/`, `Logs/`, `Exporter/`,
`Context/`) over creating a new file. Only add a new class when no existing
one covers the operation being measured.

Match the conventions already used in `test/Benchmarks` (see e.g.
`Trace/SamplerBenchmarks.cs`, `Metrics/MetricsBenchmarks.cs`):

- Namespace `Benchmarks.<Area>` matching the folder, with the standard
  Apache-2.0 SPDX header required by `AGENTS.md`.
- `[GlobalSetup]` / `[GlobalCleanup]` for constructing and disposing
  providers/exporters/`ActivitySource`s - never inside the `[Benchmark]`
  method itself.
- When comparing alternatives inside the same class, mark the existing/control
  method `[Benchmark(Baseline = true)]` per `REVIEW.md`'s Testing section.
- Add `[MemoryDiagnoser]` on the class (or pass `-EnableMemoryDiagnoser`
  /`--memory` at the CLI) whenever allocations matter - most instrumentation
  hot-path benchmarks do, per `REVIEW.md`'s Performance section.
- If the benchmark environment's processors include both performance and
  efficiency cores (e.g., on Apple M1/M2 or Intel hybrid architectures),
  consider pinning the benchmark to the performance cores to reduce noise.
  This can be achieved using the `--affinity` option in BenchmarkDotNet to
  specify an affinity mask to set for the benchmark process.

```csharp
// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;

namespace Benchmarks.<Area>;

public class MyBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        // Construct dependencies/resources here, not in a benchmark method
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Dispose of dependencies/resources here
    }

    [Benchmark]
    public void MyOperation()
    {
        // Only the operation under test
    }
}
```

Also follow the general
[BenchmarkDotNet microbenchmark design guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md)
that the .NET runtime itself follows for their own benchmarks:
no manual loops (BenchmarkDotNet iterates for you), return values to avoid
dead-code elimination, keep setup out of the measured method, and use
consistent input data across runs.

## Step 2: Run It

`benchmark.ps1` at the repo root checks out a target ref and a baseline ref
(default `main`), builds `test/Benchmarks/Benchmarks.csproj` in `Release`, and
runs both. **It requires a clean working tree** because it switches branches
- commit your change (or use a separate worktree/stash) before running it.

The selected benchmark must exist with the same name in both refs. For a new
benchmark, place the benchmark in a benchmark-only commit and use that commit
for `-Baseline`; comparing directly with `main` cannot produce a baseline
result for a benchmark that is not yet present in that branch.

```powershell
# Compare the current branch against main
./benchmark.ps1 @("*MyBenchmarks*")

# Only run the current branch, no baseline comparison
./benchmark.ps1 @("*MyBenchmarks*") -SkipBaseline

# Compare a specific branch against a specific baseline, with memory
# diagnostics, across multiple TFMs (net4xx runtimes only work on Windows)
./benchmark.ps1 @("*MyBenchmarks*") -Target my-feature -Baseline main -EnableMemoryDiagnoser -Runtimes @("net10.0", "net472")
```

Key parameters (`Get-Help ./benchmark.ps1 -Full` for the complete list):

- `-Benchmarks` (positional, required): one or more `--filter` glob patterns,
  e.g. `"*SamplerBenchmarks*"` or `"*SamplerBenchmarks.SamplerAppendingTraceState"`.
- `-Target` / `-Baseline`: refs to compare; defaults to the current branch vs.
  `main`.
- `-SkipBaseline`: only run `-Target`, skip the comparison.
- `-Job`: BenchmarkDotNet job name (e.g. `Short`) - use a short job for quick
  iteration, the default job for numbers you'll cite in a PR.
- `-Runtimes`: target frameworks to run.
- `-EnableMemoryDiagnoser` / `-EnableEventPipeProfiler`: extra BenchmarkDotNet
  diagnostics.

Artifacts land under `BenchmarkDotNet.Artifacts/<ref-name>/` at the repo root,
one subdirectory per ref, so target and baseline results don't overwrite each
other.

If you only need a single ad hoc run with no baseline comparison and don't
want the script to switch branches at all, run the project directly instead
(see `test/Benchmarks/README.md`):

```sh
dotnet run -c Release -f net10.0 --project test/Benchmarks -- --filter "*MyBenchmarks*" --memory
```

See the
[BenchmarkDotNet console args guide](https://benchmarkdotnet.org/articles/guides/console-args.html)
for the raw CLI form used by both invocations.

## Step 3: Report Results

- Per `REVIEW.md`'s Performance section, any PR claiming a performance
  improvement should include BenchmarkDotNet's GitHub-flavored-markdown table
  output substantiating the claim - paste the actual table, not a paraphrase.
  A paraphrased summary for a reviewer to glance at may be optionally included.
- Compare target and baseline from the *same* `benchmark.ps1` invocation
  (same job, same TFM, same machine state) rather than numbers from separate
  runs - results on a given machine can drift several percent run to run. If
  a delta is small enough to be in doubt, verify it with a same-session
  toggle (change, benchmark, revert, benchmark again) instead of trusting a
  single before/after pair.
- Report allocations (`Gen0`/`Allocated` columns) alongside timing for
  anything touching an instrumentation hot path - `REVIEW.md` calls out
  avoiding unnecessary allocations in code that runs on every request.

---

Build/test commands and general conventions are in
[`AGENTS.md`](../../../AGENTS.md); performance-specific review rules
(allocation guidance, `FrozenSet<T>` usage, `stackalloc` limits, benchmark
baseline marking) are in [`REVIEW.md`](../../../REVIEW.md). This skill only
covers how to produce the benchmark evidence those rules ask for.
