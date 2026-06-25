# OpenTelemetry .NET - Code Review Instructions

This file provides guidance for reviewing pull requests and local changes in this
repository. It also helps guide AI coding and review agents to make them more effective.

> [!NOTE]
> The primary audience for this document is for automated pull request review agents.

## CHANGELOG

- Every behavioural change (bug fix, new feature, breaking change) must have a
  `CHANGELOG.md` entry in the affected component's `CHANGELOG.md` file (under
  `src/<Component>/CHANGELOG.md`).
- New entries go at the **end** of the `## Unreleased` section, after all
  existing entries.
- The required format is:

  ```markdown
  * Description of the change (sentence case, ends with a period).
    ([#NNNN](https://github.com/open-telemetry/opentelemetry-dotnet/pull/NNNN))
  ```

- Use the pull request URL, not the issue URL, in the link.
- Keep the description concise and in line with the style of existing entries.
  Overly verbose entries should be shortened. Users do not usually need to know
  the implementation details, only the effect of the change.
- Do not add CHANGELOG entries for purely infrastructure changes (CI workflow
  updates, documentation-only fixes, test changes, Renovate dependency updates,
  etc.).
- If a single PR modifies multiple components, each affected component's
  `CHANGELOG.md` needs its own entry with the same PR link.
- "Breaking change" entries should be prefixed with `**Breaking Change**:`.

## NuGet Package References

- All package versions are centralized in `Directory.Packages.props`. Flag any
  `Version="..."` attribute on a `<PackageReference>` in a src or test `.csproj`
  file. `VersionOverride` is allowed only for intentional version-range pinning
  (e.g. `VersionOverride="[X.Y.Z,)"` for third-party packages in new components
  to prevent Renovate from automatically bumping the dependency onto library
  consumers).
- Never add `<Version>` to a project file; use `Directory.Packages.props`.

## Public API Surface

- Any new or modified public type or member must be declared in the component's
  `.publicApi/PublicAPI.Unshipped.txt` file. A build error is raised if this is
  missing.
- Flag public API changes that are not reflected in `PublicAPI.Unshipped.txt`.
- Breaking API changes (removals, signature changes, type moves) are not allowed
  without an explicit maintainer decision and a breaking-change CHANGELOG entry.
- For a new component, `PublicAPI.Shipped.txt` should be empty (contains only
  `#nullable enable`) and all initial surface goes in `Unshipped.txt`. Entries
  are moved to `Shipped.txt` automatically during the release process.

## Experimental APIs

Experimental APIs require additional process beyond code review:

- Every experimental feature must be assigned a `OTEL####` diagnostic ID (see
  `src/Shared/DiagnosticDefinitions.cs`).
- A corresponding markdown doc file must be added to
  `docs/diagnostics/experimental-apis/` and the feature listed in the `README.md`
  in that directory.
- New packages that include experimental APIs must be registered in
  `.github/security-insights.yml` and referenced in `build/RELEASING.md`.
- Experimental features should be discussed at the next OpenTelemetry .NET SIG
  meeting with the maintainers before a PR is opened to agree need and direction.

## XML Documentation Comments

- Use `<para/>` as the paragraph separator in XML doc comments - not a blank
  line inside the `///` block.
- For `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException`
  thrown from a property setter, the `paramName` argument should be
  `nameof(value)`, not the property's name.
- When writing XML doc comments, write idiomatic, readable English rather than
  copying specification text verbatim. If the underlying spec text is not
  self-explanatory, rephrase it. Include links to specification documents where
  appropriate using permalinks (i.e. to tags or git SHAs - avoid branch names
  like `main`).
- Use `<c>TypeName</c>` for type/member references inline in text when not using
  a `<see cref="..."/>` link.
- In-code `// TODO` comments must reference a full issue URL (e.g.
  `// TODO: https://github.com/open-telemetry/opentelemetry-dotnet/issues/NNNN`),
  not just a number or text description.

## Banned APIs

The repository bans several APIs via `build/BannedSymbols.txt`. A Roslyn
analyzer catches them at build time, but flag them in review too:

### Unsafe code

- **Do not use** `System.Runtime.CompilerServices.Unsafe` or
  `System.Runtime.InteropServices.MemoryMarshal` - both are banned. The
  repository is actively working to eliminate existing `unsafe` code; do not
  introduce new usages, regardless of performance benefit.
- The only remaining `unsafe` usages target non-`net*` TFMs. Do not expand
  unsafe code to `#if NET` code paths.

### String comparisons and culture-sensitive parsing

- **Do not use** the instance `string.Equals(string)` or
  `string.Equals(string, StringComparison)` methods - use the **static**
  `string.Equals(a, b)` / `string.Equals(a, b, StringComparison)` overloads to
  avoid potential `NullReferenceException`.
- **Do not use** the culture-sensitive `TryParse(string, out T)` or
  `TryParse(ReadOnlySpan<char>, out T)` overloads on numeric/date types - always
  pass `CultureInfo.InvariantCulture` (or `NumberStyles` + `IFormatProvider`).

### Async code

- **Do not use** `Task<T>.Result` - use `await` or `GetAwaiter().GetResult()`
  instead. `Task<T>.Result` is banned to prevent deadlocks.

### ActivitySource and Meter construction

- **Do not use** the `ActivitySource(string)`, `ActivitySource(string, string)`,
  or `ActivitySource(string, string, IEnumerable<...>)` constructors - use the
  `ActivitySource(ActivitySourceOptions)` overload instead.
- **Do not use** the `Meter(string)`, `Meter(string, string)`, or
  `Meter(string, string, IEnumerable<...>, object)` constructors - use the
  `Meter(MeterOptions)` overload instead.

### HttpClient helpers

- **Do not use** `HttpClient.GetByteArrayAsync`, `HttpClient.GetStringAsync`,
  `HttpContent.ReadAsByteArrayAsync`, or `HttpContent.ReadAsStringAsync` - use
  the corresponding methods on `HttpClientHelpers` instead.

## Code Correctness

### Memory and caching

- Avoid using a `static` `ConcurrentDictionary<TKey, TValue>` (or similar
  unbounded static cache) keyed on object instances (e.g. `Resource`, `Metric`,
  `ActivitySource`). A static cache strongly references every key it ever sees,
  including those from disposed providers, causing memory leaks.
- Prefer `ConditionalWeakTable<TKey, TValue>` for per-instance caching - it
  allows keys to be garbage-collected when no other live references remain. On
  .NET use the `GetValue` / `GetOrCreateValue` API; on older TFMs use the
  `GetOrCreateValue` pattern.
- Alternatively, cache computed values as fields on the owning object (e.g. the
  exporter or provider), so the cache lifetime is naturally bound to the owning
  object's lifetime.

### Infinite loops

`while (true)` loops that do not have a provably finite exit path must include a
safety mechanism - a `maxAttempts` counter, a `Stopwatch`-based timeout, or
similar - and throw an exception or break if the limit is exceeded.

### Error handling in OpenTelemetry components

- For exporters: document (and where possible test) what happens when internal
  buffer or size limits are exceeded.

## Performance

- When making performance optimizations, measure before and after using the
  `benchmark.ps1` script in the repository root. This script runs BenchmarkDotNet
  and compares the branch under review against `main`.
- Any PR that claims a performance improvement should include benchmark results
  (BenchmarkDotNet's GitHub output) to substantiate the claim.
- For hot-path string or value formatting, consider returning cached constant
  results for well-known common values before falling back to a runtime
  formatting call.
- Avoid unnecessary allocations in instrumentation code that runs on every
  request.
- For `stackalloc`, keep the size at or below 256 bytes to match the convention
  used in the .NET runtime repository. If a larger buffer may be needed, use
  `ArrayPool<byte>.Shared` (cache the reference to `ArrayPool<byte>.Shared` in
  a local variable rather than re-accessing the property on every use).
- Use `FrozenSet<T>` instead of `HashSet<T>` on `#if NET` code paths when the
  set's contents are fixed after construction. Use a preprocessor guard:

  ```csharp
  #if NET
      this.MySet = FrozenSet.ToFrozenSet(items, StringComparer.Ordinal);
  #else
      this.MySet = new HashSet<string>(items, StringComparer.Ordinal);
  #endif
  ```

- Prefer the `char` overload of `string.Replace(char, char)` over the string
  overload when replacing a single character.
- Pre-size `List<T>` and `Dictionary<TKey, TValue>` with an estimated initial
  capacity when the number of elements is known or can be approximated. Do not
  use such values for pre-sizing if the value comes from untrusted input
  (e.g. a network request) - this can be exploited to cause excessive memory allocation.
- Use `StringBuilder` for string construction in loops rather than repeated
  string concatenation.

## Testing

- When using `Assert.NotNull(x)` or similar null-guard assertions, do not add
  the null-forgiving `!` operator on the same value in subsequent assertions -
  the guard already expresses the contract.
- Include the actual value in assertion failure messages where appropriate, e.g.
  `Assert.True(condition, $"Expected ... but got {actualValue}")`.
- Use `[Obsolete("...")]` on test methods that exercise obsolete APIs instead of
  suppressing the compiler warning via `#pragma`.
- When testing a refactoring (e.g. moving types between files), prefer testing
  behaviour through the public API rather than testing that internal types still
  exist.
- Where relevant, prefer snapshot-style tests that assert the entire output of a
  method rather than asserting individual properties. This is particularly
  important for serialization-related tests such as for OTLP and Prometheus output.
- If a PR fixes a bug, it should include a regression test that would have failed
  before the fix.
- For new non-trivial features or changes, include tests that cover the new
  behaviour, including error/boundary conditions.
- Benchmark methods that serve as a performance baseline should be marked with
  `[Benchmark(Baseline = true)]` where appropriate.

## API Design Patterns

- Prefer `ReadOnlySpan<byte>` over `ReadOnlyMemory<byte>` for constructor
  parameters when the data is only needed at construction time and is snapshotted
  internally (avoids a forced heap allocation at the call site).
- Use `left` and `right` as parameter names for `==`/`!=` operator overloads and
  `Equals(T other)` methods.
- On .NET targets (`#if NET`), use `System.HashCode` (the struct API with
  `HashCode.Combine(...)` or `Add`/`ToHashCode`) rather than the manual
  `unchecked { h1 * 31 ^ h2 }` pattern.
- Value types that are compared for equality should implement `IEquatable<T>` in
  addition to overriding `Equals(object?)` and `GetHashCode()` if they are not
  `record` types.
- Prefer to use primary constructors where appropriate.
- Use expression-bodied members where they improve readability.
- Prefer C# pattern matching over chains of `if`/`else if` when dispatching on
  type or value.
- When adding new `[GeneratedRegex(...)]` attributes, always specify
  `matchTimeoutMilliseconds` with an appropriate value to guard against
  catastrophic backtracking.
- Use `#if NET` (not `#if NET8_0_OR_GREATER` or similar) when guarding code for
  modern .NET vs. older TFMs where all supported TFMs of that version are included.
- When checking OS platform on older TFMs, use
  `System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.X)`
  inside an `#else` block; on `#if NET`, use `OperatingSystem.IsWindows()` etc.

## New Packages Checklist

When a new NuGet package is introduced, in addition to the standard `AGENTS.md`
checklist:

- Add the package to `.github/security-insights.yml` (in the packages array).
- Add the package to `build/RELEASING.md` (in the appropriate stable/unstable
  list).
- For third-party dependencies, use `VersionOverride="[X.Y.Z,)"` for minimum
  version pinning so Renovate updates are applied to the test projects but the
  lower bound exposed to library consumers is not silently raised.

## Pull Request Hygiene

- PRs should be focused and small. A large number of changed lines in a single PR
  makes meaningful review impractical - suggest splitting by concern.
- Do not merge upstream `main` into a PR branch repeatedly just to stay current;
  this generates unnecessary notifications for reviewers.
- All contributors must have signed the
  [EasyCLA](https://easycla.lfx.linuxfoundation.org/) agreement before a PR can
  be merged. Flag any PR where the CLA check has not passed. Maintainers will
  generally not review changes before the CLA is signed.
- For new features or non-trivial changes, the design should be discussed in a
  GitHub issue **before** a PR is opened, and significant API additions should be
  raised at the next OpenTelemetry .NET SIG meeting.

## Line Length and Readability

- Lines that wrap in the GitHub web view (roughly >120 characters) should be
  broken at a logical boundary. This applies equally to code, XML doc comments,
  and CHANGELOG entries.
- Method signatures with many parameters are easier to read when each parameter
  is on its own line.
- In `Directory.Packages.props`, `OpenTelemetry.slnx`, and similar list files,
  keep entries sorted alphabetically.

## PowerShell Scripts

- Use `Write-Warning` for non-fatal advisory output, and `Write-Error` (or
  `throw`) for failures, rather than plain `Write-Host`. This allows callers and
  CI to distinguish severity.
- Cross-platform PowerShell scripts should start with `#!/usr/bin/env pwsh`.
- Aggregate errors and report them at the end of a script (fail-at-end pattern)
  rather than stopping at the first warning, so that all issues are visible in
  one run.
- New scripts should include comment-based help (`.SYNOPSIS`, `.DESCRIPTION`,
  `.EXAMPLE`) so that `Get-Help` returns useful output.

## What NOT to Flag

To maintain a high signal-to-noise ratio, **do not comment on**:

- Code style, formatting, or whitespace that is already enforced by
  `dotnet format` / StyleCop / `.editorconfig` - CI will catch those
  automatically.
- Renovate-managed dependency update PRs - those are auto-generated.
- `otelbot` automated PRs (semantic-conventions sync) unless there is an obvious
  functional defect.
- Comments suggesting code that will not compile - the compiler and CI enforce
  this, and such comments risk false positives where new language features are
  used that AI models may not be aware of.

## Miscellaneous

- Where possible, use more-performant APIs added in newer releases of .NET and
  use preprocessor directives to maintain compatibility with older versions of
  .NET, provided that the added complexity is justified by the gain. For example,
  use `FrozenSet<T>` instead of `HashSet<T>` on `#if NET` code paths when the
  set is read-only after construction.
- Where appropriate, links to external documentation (Microsoft Learn, GitHub,
  etc.) should not include language-specific URL slugs (e.g. `/en/`, `/en-us/`)
  so that documentation is shown to readers in their preferred browser language.
- When reviewing changes related to OpenTelemetry semantic conventions, refer to
  the official specification at <https://github.com/open-telemetry/semantic-conventions>
  and provide citations to justify suggestions. Do not provide speculative
  feedback without referencing the official specification.
- Issue and PR references in source code comments should use the full URL, not
  just the number, to aid searchability (e.g.
  `https://github.com/open-telemetry/opentelemetry-dotnet/issues/NNNN`).
