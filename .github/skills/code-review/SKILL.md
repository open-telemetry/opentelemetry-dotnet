---
name: code-review
description: Review code changes in opentelemetry-dotnet for correctness, conventions, and consistency with AGENTS.md and REVIEW.md. Use when reviewing PRs or code changes.
---

# opentelemetry-dotnet Code Review

Review code changes against the conventions and process documented in
[`AGENTS.md`](../../../AGENTS.md) and [`REVIEW.md`](../../../REVIEW.md) at the
repository root. Those two files are the single source of truth for build/test
commands, architecture, coding conventions, banned APIs, CHANGELOG format, the
public API process, and what not to flag - this skill does not repeat their
content. Load them in full before reviewing; if anything below conflicts with
them, they win.

**Reviewer mindset:** Be polite but skeptical. Your job is to speed up
maintainer review by finding problems the author may have missed and by
questioning whether the change is justified at all. Treat the PR description
and linked issues as claims to verify, not facts to accept.

## When to Use This Skill

- Reviewing a PR or local diff in this repository.
- Checking code for correctness, conventions, or CHANGELOG/public-API
  completeness before opening a PR.
- Asked to review, critique, or provide feedback on a change.

## Review Process

### Step 0: Load the Rules

Read `AGENTS.md` and `REVIEW.md` in full. Between them they cover: build/test
commands, the three-layer architecture (API / SDK / Exporters), package and
versioning rules, `.publicApi/` tracking, the experimental API process, banned
APIs (`build/BannedSymbols.txt`), CHANGELOG format, and the "What NOT to Flag"
list. Cite the specific rule a finding violates rather than restating the rule
set in your output.

If the repository defines area-specific review agents under `.github/agents/`,
check for one matching the changed component and invoke it as a subtask,
integrating its findings. None exist in this repository today, so unless one
has since been added, continue the review yourself.

### Step 1: Gather Code Context Before Reading the PR Narrative

Do **not** read the PR description, linked issues, or existing review
comments yet - form an independent view first so the author's framing doesn't
anchor your judgment.

1. **Diff and file list**: fetch the full diff and changed-file list.
2. **Full source files**: read beyond the diff hunks for every changed file -
   understand the surrounding class, its invariants, and how it's
   constructed/disposed. A `BaseExporter<T>` / `BaseProcessor<T>` / `Sampler`
   subclass needs the base class contract read too, not just the override.
3. **Consumers and callers**: for a changed public or internal API, grep for
   callers and test sites (`src/`, `test/`, and any `examples/` using it) to
   see if the change could break an existing assumption.
4. **Sibling components**: if the change fixes a bug or adds a pattern in one
   exporter/processor/instrumentation, check whether sibling components
   (other exporters for the same signal, other TFM-specific `#if` branches)
   need the same fix.
5. **Shared code**: if the diff touches `src/Shared/*`, remember it is
   **linked** (`<Compile Include="..." Link="..." />`), not referenced, into
   every consuming project - check the effect on each consumer, not just one.
6. **Git history**: `git log --oneline -20 -- <file>` for changed files - look
   for reverts or recent related churn in the area.
7. **CHANGELOG and public API surface**:
   - Confirm every behavioral change has a `CHANGELOG.md` entry in each
     affected component, in the format `AGENTS.md`/`REVIEW.md` specify.
     Performance improvements do not necessarily require a CHANGELOG entry
     as they can be considered internal refactoring, but any behavioral change
     does.
   - Diff each touched component's `.publicApi/PublicAPI.Unshipped.txt` (and
     `.publicApi/Experimental/PublicAPI.Unshipped.txt` for `[Experimental]`
     APIs) against the new/changed public surface in the diff. A missing
     entry is a build error, but flag it explicitly anyway - CI catching it
     doesn't mean the rest of the PR is correct.
   - If new experimental API is introduced, verify the `OTEL####` diagnostic
     ID, the `docs/diagnostics/experimental-apis/` doc, and the
     `.github/security-insights.yml` / `build/RELEASING.md` registration
     described in `REVIEW.md`.

### Step 2: Form an Independent Assessment

Based only on the code gathered above, answer:

1. What does this change actually do - old behavior vs. new behavior?
2. Why might it be needed? Infer the motivation from the code itself.
3. Is this the right approach - is there a simpler alternative consistent with
   the three-layer architecture, or existing functionality that already
   solves it?
4. What problems do you see - bugs, missed TFMs, thread-safety, allocations on
   a hot path, missing tests, missing CHANGELOG entries?

Write this down before reading the PR narrative.

### Step 3: Incorporate the PR Narrative and Reconcile

Now read the PR description, labels, linked issues, and existing review
comments as claims to verify, not facts.

- If the PR claims a bug fix or a performance improvement, verify it against
  the code and any benchmark evidence (see the `performance-benchmark` skill
  for what that evidence should look like).
- Where your independent read disagrees with the author's framing, investigate
  rather than deferring - a problem your independent assessment found is not
  invalidated just because the description doesn't mention it.
- Check whether the design was discussed in a GitHub issue first, and whether
  significant API additions were raised at the OpenTelemetry .NET SIG, per
  `REVIEW.md`'s Pull Request Hygiene section.
- Check the EasyCLA status; per `REVIEW.md`, maintainers generally won't
  review a PR before it passes.

### Step 4: Detailed Analysis

1. Prioritize bugs, thread-safety, resource lifetime (`IDisposable`),
   allocation regressions on instrumentation hot paths, and API design over
   style - `dotnet format`, StyleCop, and the sanity-check script already
   catch style, formatting, and non-ASCII/whitespace issues; don't repeat what
   CI enforces.
2. Consider collateral damage: for every changed path, check other callers,
   other TFMs (especially `net462`/`netstandard2.0` `#else` branches), and
   other signals (traces/metrics/logs) that share the touched code.
3. Be specific: cite the file/line, the exact rule from
   `AGENTS.md`/`REVIEW.md` it violates, and how you verified the concern is
   real (e.g. "checked all `AddSource` callers in `test/` - none pass a null
   tag").
4. Flag severity:
   - :x: **error** - must fix: bugs, a bug fix missing its regression test,
     banned API usage, a missing CHANGELOG or public-API entry, a breaking
     API change without an explicit maintainer decision.
   - :warning: **warning** - should fix: a performance claim without benchmark
     evidence, missing test coverage for new behavior, inconsistency with an
     established pattern.
   - :bulb: **suggestion** - consider: style/readability, an optional
     optimization.
5. Don't pile on: raise a recurring issue once, listing all affected files.
6. Apply `REVIEW.md`'s "What NOT to Flag" list literally - don't flag
   formatting, Renovate PRs, or `otelbot` semantic-convention syncs without an
   obvious functional defect.

## Review Output Format

> :memo: **AI-generated content disclosure:** when posting review content to
> GitHub under a user's own credentials (not a dedicated bot/app account),
> include a visible `> [!NOTE]` disclosure that the content is AI-generated,
> unless the user explicitly asks to omit it.

```markdown
## PR Review

**Motivation**: <1-2 sentences on whether the problem is real and the PR is justified>

**Approach**: <1-2 sentences on whether the approach fits the three-layer architecture and existing conventions>

**Summary**: <:white_check_mark: LGTM / :warning: Needs Human Review / :warning: Needs Changes / :x: Reject>. <2-3 sentence summary>

---

### Detailed Findings

#### :white_check_mark: / :warning: / :x: <Category> - <Brief description>

<Explanation, citing file/line and the specific AGENTS.md/REVIEW.md rule.>

<!-- AI disclosure note goes below this line when posting under a personal account. -->
```

### Verdict Rules

- The verdict must reflect the most severe finding - any :warning: rules out a plain
  "LGTM".
- When unsure whether a concern is valid, escalate to "Needs Human Review"
  rather than guessing either way.
- A change can have correct code but an incomplete approach (e.g. it fixes one
  exporter but not its siblings, or masks a symptom); reflect that gap in the
  verdict even if the diff itself is clean.
- Before finalizing, re-read every :warning: / :x: finding and ask "would I be
  comfortable if this merged as-is?" - if no or unsure, the verdict cannot be
  LGTM.

## Multi-Model Review (Optional)

If the environment supports launching sub-agents on different model families,
run the review in parallel across 2-3 models for a substantial or risky
change, then synthesize: deduplicate findings, elevate ones flagged by
multiple models, and include unique high-confidence findings. Skip this for
small or low-risk changes - it multiplies review cost by the number of models
used.

---

Build/test commands, banned APIs, the CHANGELOG format, the public API
process, testing conventions, and the full "what not to flag" list live in
[`AGENTS.md`](../../../AGENTS.md) and [`REVIEW.md`](../../../REVIEW.md).
Reload them if this skill's guidance seems to conflict; they take precedence.
