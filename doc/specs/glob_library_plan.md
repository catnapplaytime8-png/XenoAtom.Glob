# XenoAtom.Glob Implementation Plan

This plan is the execution checklist for the specification in [glob_library_specs.md](./glob_library_specs.md).

The intent is to give an implementer a concrete sequence of work, explicit quality gates, and a test strategy strong enough to establish high confidence in correctness, performance, and long-term maintainability.

## 1. Delivery Rules

- [x] Treat [glob_library_specs.md](./glob_library_specs.md) as the behavioral contract.
- [x] Keep diffs focused and land work in self-contained steps.
- [x] Add tests before or with each behavior change.
- [x] Keep `readme.md`, `doc/readme.md`, and the spec docs in sync with shipped behavior.
- [x] Do not defer correctness gaps behind undocumented TODO behavior.
- [x] Run the full test suite before closing each milestone.

## 2. Milestone Overview

- [x] Milestone 0: Development foundation and quality harness
- [x] Milestone 1: Path model and normalization primitives
- [x] Milestone 2: Core glob parser and matcher
- [x] Milestone 3: `.gitignore` parser and rule compilation
- [x] Milestone 4: Ignore evaluation engine and explanation model
- [x] Milestone 5: Git-aware repository discovery
- [x] Milestone 6: File tree walker and traversal pruning
- [x] Milestone 7: Performance tuning and benchmark hardening
- [x] Milestone 8: Additional ignore dialect hooks
- [ ] Milestone 9: API review, documentation, and release readiness

## 3. Milestone 0: Development Foundation And Quality Harness

- [x] Create the initial public API sketch in code comments or stub types so the implementation has a stable target.
- [x] Establish namespace layout under `XenoAtom.Glob`, `XenoAtom.Glob.Ignore`, `XenoAtom.Glob.IO`, and `XenoAtom.Glob.Git`.
- [x] Replace placeholder files in the library and tests with meaningful structure.
- [x] Set up test helper infrastructure for temporary directories, temporary repositories, and path corpus generation.
- [x] Add a benchmark project or benchmark folder strategy for later BenchmarkDotNet coverage.
- [x] Add shared test utilities for path normalization, file layout creation, and Git CLI invocation.
- [x] Add CI expectations for unit tests, Git differential tests, and benchmarks that are safe to run in CI.
- [x] Require Git to be present in CI on supported platforms and fail fast with a clear message when the differential suite cannot run in environments where it is expected.
- [x] Add a small test helper that captures and prints `git --version` in differential test runs.

Exit criteria:

- [x] The repo has a clean test harness and no placeholder production types blocking implementation.
- [x] Developers can create isolated file-system and repository test fixtures with minimal boilerplate.

## 4. Milestone 1: Path Model And Normalization Primitives

- [x] Define the internal relative path contract described in [glob_library_specs.md](./glob_library_specs.md).
- [x] Implement path normalization that converts separators to `/` without using expensive general-purpose path APIs in hot paths.
- [x] Encode directory/file distinction separately from the path text.
- [x] Implement validation rules for relative paths used by the matcher and ignore engine.
- [x] Add low-allocation helpers for slicing segments from normalized paths.
- [x] Decide and document the internal comparison strategy abstraction needed for case-sensitive versus case-insensitive scenarios.

Tests:

- [x] Unit tests for separator normalization on Windows-style and Unix-style input.
- [x] Unit tests for duplicate separators, `.` segments, empty segments, and trailing separator handling.
- [x] Unit tests for directory flag handling independent of trailing slash input.
- [x] Unit tests for invalid input rejection behavior.
- [x] Property-style tests for path normalization invariants on random segment combinations.

Exit criteria:

- [x] All path consumers can operate on a normalized path representation without calling `Path.GetRelativePath`.
- [x] Hot-path helpers have allocation behavior measured and documented.

## 5. Milestone 2: Core Glob Parser And Matcher

- [x] Implement `GlobPattern` parsing for literals, `*`, `?`, character classes, escapes, and `**`.
- [x] Define a stable representation for compiled tokens and compiled segments.
- [x] Implement specialized matchers for literal path, literal name, suffix, prefix, single-segment wildcard, and general recursive glob.
- [x] Add directory-aware matching hooks needed by ignore semantics.
- [x] Define invalid-pattern behavior and expose it through parse results rather than exceptions in normal control flow.
- [x] Add a small diagnostic representation for parser debugging that is not used in hot paths.

Tests:

- [x] Unit tests for exact literal matches and mismatches.
- [x] Unit tests for `*`, `?`, and character class semantics.
- [x] Unit tests for escaped wildcard characters and escaped metacharacters.
- [x] Unit tests for `**/foo`, `foo/**`, `a/**/b`, and adjacent wildcard combinations.
- [x] Unit tests for segment-boundary behavior where `*` and `?` must not cross `/`.
- [x] Unit tests for invalid bracket expressions and trailing backslash handling.
- [x] Property-style tests comparing specialized matcher results against the general matcher on generated cases.
- [x] Microbenchmarks for compile cost and single-pattern match throughput.

Exit criteria:

- [x] The glob engine is usable without any ignore-file infrastructure.
- [x] Specialized and fallback matchers produce identical results for the same semantic input.

## 6. Milestone 3: `.gitignore` Parser And Rule Compilation

- [x] Implement `.gitignore` line parsing with support for comments, escaped comments, negation, trailing slash, and escaped trailing spaces.
- [x] Preserve source metadata: source file, source kind, line number, original pattern text.
- [x] Support LF and CRLF inputs and files without a trailing newline.
- [x] Compile parsed ignore rules into immutable rule objects using the core glob engine.
- [x] Capture anchoring mode and directory-only behavior explicitly rather than inferring them at match time.
- [x] Provide a parser API that can read from text, spans, or streams without forcing the caller into one input model.

Tests:

- [x] Unit tests for blank lines, comments, escaped `#`, escaped `!`, and escaped spaces.
- [x] Unit tests for leading slash, middle slash, no slash, and trailing slash semantics at parse level.
- [x] Unit tests for line numbering and source metadata preservation.
- [x] Unit tests for invalid lines and invalid escape endings.
- [x] Corpus tests with curated `.gitignore` snippets covering common and obscure forms.

Exit criteria:

- [x] Every parsed rule retains enough metadata to explain a winning match later.
- [x] Parse output is immutable and reusable across many match operations.

## 7. Milestone 4: Ignore Evaluation Engine And Explanation Model

- [x] Implement ordered rule evaluation with last-match-wins behavior.
- [x] Implement layered rule sets with precedence matching the spec.
- [x] Implement per-directory relative evaluation semantics.
- [x] Implement negation behavior and the no-reinclude-below-pruned-directory rule.
- [x] Add `IgnoreEvaluationResult` with a minimal fast-path shape and optional expanded diagnostics.
- [x] Design `IgnoreStack` push/pop mechanics so descending into directories does not rebuild all parent state.

Tests:

- [x] Unit tests for single-rule include and exclude outcomes.
- [x] Unit tests for multiple-rule ordering within one file.
- [x] Unit tests for precedence between parent `.gitignore`, child `.gitignore`, `.git/info/exclude`, and global excludes.
- [x] Unit tests for directory-only matches versus file matches.
- [x] Unit tests for basename-only patterns matching at any depth under the rule base.
- [x] Unit tests for negation and re-inclusion in reachable directories.
- [x] Unit tests proving that excluded parent directories prevent child re-inclusion from taking effect.
- [x] Unit tests for explanation payload: winning rule, source file, line number, and raw pattern.

Differential tests:

- [x] Introduce a Git-backed differential suite using `git check-ignore --no-index -v`.
- [x] Add batch-oriented differential helpers using `git check-ignore --no-index --stdin -z -v --non-matching`.
- [x] Compare both the ignore decision and the winning rule metadata against Git.
- [x] Include cases for `.gitignore`, `.git/info/exclude`, and `core.excludesFile`.
- [x] Include cases with nested `.gitignore` files and conflicting rules.
- [x] Include cases with CRLF rule files, escaped spaces, escaped leading `!`, and complex `**` patterns.
- [x] Include cases that intentionally probe documented Git limitations such as failed re-inclusion below excluded directories.
- [x] Store fixture definitions in a reusable corpus format so regressions can be reproduced exactly.

Exit criteria:

- [x] The engine matches Git for the curated compatibility corpus supported by the milestone.
- [x] Explanation mode reproduces enough metadata to diagnose mismatches against Git.

## 8. Milestone 5: Git-Aware Repository Discovery

- [x] Implement discovery of a repository root from an arbitrary working-tree path.
- [x] Support both `.git` directories and `.git` gitfiles containing `gitdir: <path>`.
- [x] Resolve `.git/info/exclude` from discovered metadata.
- [x] Resolve the global exclude file from Git configuration or from an explicitly supplied resolved path, depending on the chosen API decision.
- [x] Resolve repository case-comparison behavior from `core.ignorecase` when configured.
- [x] Make repository discovery explicit and reusable rather than burying it inside traversal.
- [x] Document any intentionally deferred repository behaviors.

Tests:

- [x] Unit tests for repository discovery when `.git` is a directory.
- [x] Unit tests for repository discovery when `.git` is a gitfile with a relative path.
- [x] Unit tests for worktree-like metadata layouts.
- [x] Unit tests for submodule-like gitfile layouts.
- [x] Unit tests for missing or malformed gitfile content.
- [x] Differential tests proving the discovered root, git dir, and ignore sources match what Git reports for the same temporary repository.
- [x] Validate repository discovery against `git rev-parse --show-toplevel` and `git rev-parse --git-dir`.

Exit criteria:

- [x] Repository discovery is correct for normal repositories, linked worktrees, and gitfile-backed working trees in the supported scenarios.

## 9. Milestone 6: File Tree Walker And Traversal Pruning

- [x] Implement traversal around `FileSystemEnumerable<T>` or the chosen equivalent.
- [x] Design `FileTreeEntry` so it exposes enough metadata without forcing expensive object creation.
- [x] Evaluate ignore rules before descending into directories.
- [x] Load per-directory `.gitignore` lazily when entering a directory.
- [x] Implement pruning of excluded directories.
- [x] Keep non-following symlink or reparse-point behavior as the default Git-compatible mode.
- [x] Add cancellation support to long-running walks.
- [x] Decide and document whether ordering is unspecified or optionally configurable.

Tests:

- [x] Unit tests for flat directory enumeration with no ignore rules.
- [x] Unit tests for nested traversal with parent and child `.gitignore` files.
- [x] Unit tests proving excluded directories are pruned and not descended into.
- [x] Unit tests proving reachable directories can still re-include children when allowed.
- [x] Unit tests for `.gitignore` loading only from directories actually entered.
- [x] Unit tests for symlink or reparse-point handling where supported by the platform.
- [x] Integration tests over realistic directory trees with mixed files, directories, and ignore rules.
- [x] Differential traversal tests that compare the resulting visible path set to a Git-derived expected set in supported scenarios.
- [x] Differential traversal tests that also compare per-entry ignore decisions against `git check-ignore` for the same fixture paths.

Performance tests:

- [x] Benchmarks for traversal with no ignore rules.
- [x] Benchmarks for traversal with shallow ignore stacks.
- [x] Benchmarks for traversal with deep nested ignore stacks.
- [x] Benchmarks for traversal where most entries are pruned early.

Exit criteria:

- [x] Traversal is correct, lazy, and prunes excluded directories before descent.
- [x] The walk API is usable independently of the Git-specific layer.

## 10. Milestone 7: Performance Tuning And Benchmark Hardening

- [x] Establish representative benchmark corpora: small, medium, large, deep, wildcard-heavy, and ignore-heavy.
- [x] Measure allocations and throughput for path normalization, pattern compilation, matching, ignore evaluation, and traversal.
- [x] Identify high-allocation hot spots with profiler-backed inspection.
- [x] Cache parsed ignore files by path plus file metadata when repository contexts are reused.
- [x] Introduce pooling or data-structure changes only when measurement justifies them.
- [x] Verify optimizations do not change semantics by running the full correctness suite after each tuning step.
- [x] Document expected performance characteristics and known tradeoffs.

Benchmark gates:

- [x] Benchmark results are reproducible enough to detect regressions.
- [x] Allocation counts for skipped entries remain near-zero in the intended steady state.
- [x] No optimization lands without a before-and-after measurement.

Notes:

- [x] The dedicated skipped-entry traversal benchmark confirms that ignored entries are skipped with genuinely negligible managed allocations in steady-state runs.

## 11. Milestone 8: Additional Ignore Dialect Hooks

- [x] Extract the parser and rule-evaluation seams needed for additional dialects without weakening `.gitignore` correctness.
- [x] Add internal or public dialect abstractions only if they stay small and concrete.
- [x] Prototype the first follow-up dialect support path, likely `.ignore` or `.dockerignore`, behind a focused adapter model.
- [x] Document exactly what is shared and what remains dialect-specific.

Tests:

- [x] Unit tests proving `.gitignore` semantics remain unchanged after introducing dialect extension points.
- [x] Unit tests for any newly introduced dialect adapter behavior.

Exit criteria:

- [x] `.gitignore` remains the reference-quality path while extension points stay restrained.

## 12. Milestone 9: API Review, Documentation, And Release Readiness

- [x] Review public API naming, overload shape, XML docs, and discoverability.
- [x] Remove placeholder or experimental types that should not ship.
- [x] Add XML docs for all public APIs, including exceptions and behavioral caveats.
- [x] Update `readme.md` with the final capability set and usage examples.
- [x] Update `doc/readme.md` with a more detailed guide for globbing, ignore evaluation, and traversal.
- [x] Cross-check this plan and the spec against the actual implementation and mark deferred items explicitly.
- [x] Add examples for standalone glob use, standalone ignore use, and repository traversal use.
- [x] Review AOT and trimming compatibility warnings and resolve or document them.

Release gate:

- [ ] All tests pass locally and in CI.
- [x] Differential compatibility suite is green.
- [x] Benchmark results are recorded for the release candidate.
- [x] Docs match the shipped API and behavior.

## 13. Top-Confidence Test Plan

This section is the quality backbone for the project and should be treated as mandatory work, not optional hardening.

### 13.1 Unit Test Matrix

- [x] Path normalization tests
- [x] Glob parser tests
- [x] Glob matcher tests
- [x] `.gitignore` parser tests
- [x] Ignore rule precedence tests
- [x] Negation and pruning tests
- [x] Repository discovery tests
- [x] Traversal tests
- [x] Public API guard tests for argument validation and XML-doc-covered behavior

### 13.2 Differential Compatibility Matrix

- [x] Compare ignore decisions to `git check-ignore --no-index`.
- [x] Compare winning source file, line number, and pattern text to Git verbose output.
- [x] Compare batch query behavior using `git check-ignore --no-index --stdin -z -v --non-matching`.
- [x] Cover nested `.gitignore` files with overrides.
- [x] Cover `.git/info/exclude`.
- [x] Cover `core.excludesFile`.
- [x] Cover worktree and gitfile repository layouts.
- [x] Cover line ending variants and escaped-space cases.
- [x] Cover `core.ignorecase=true` and `core.ignorecase=false` repository behavior.
- [x] Cover case-sensitivity behavior on each supported platform.
- [x] Capture the Git version in test output and in failure diagnostics.
- [x] Fail the compatibility gate if Git and the library disagree on a supported scenario.

### 13.3 Cross-Platform Matrix

- [x] Run the full suite on Windows.
- [x] Run the full suite on Linux.
- [ ] Run the full suite on macOS.
- [x] Ensure platform-conditional expectations are explicit and localized in the tests.
- [x] Add targeted tests for separator handling and platform-specific path edge cases.

### 13.4 Fuzz And Property Coverage

- [x] Add property-style tests for normalization and matcher invariants.
- [x] Add parser fuzz-style inputs for malformed bracket expressions, dangling escapes, unusual whitespace, and repeated separators.
- [x] Add generated corpus tests that compare specialized matchers with the fallback matcher.
- [x] Add generated ignore corpora for rule-order and negation invariants.

### 13.5 Realistic Integration Corpora

- [x] Build reusable file-tree fixtures that look like real repositories with nested source, build, temp, package, and tool output directories.
- [x] Add deep-tree fixtures to exercise pruning and stack behavior.
- [x] Add fixtures with large sibling counts to stress enumeration.
- [x] Add fixtures with many small `.gitignore` files to stress rule-stack updates.

### 13.6 Performance Regression Coverage

- [x] Benchmark pattern compilation throughput.
- [x] Benchmark single-path match throughput.
- [x] Benchmark ignore evaluation throughput with 1, 10, 100, and 1000 effective rules.
- [x] Benchmark traversal throughput on small, medium, and large trees.
- [x] Track allocations for hot scenarios.
- [x] Add regression thresholds or recorded baselines where CI noise permits.

### 13.7 Failure Analysis Tooling

- [x] Ensure test failures print enough diagnostics to compare the library result and Git result quickly.
- [x] Provide helper assertions that print normalized path, directory flag, winning rule metadata, and evaluated rule stack.
- [x] Preserve temporary fixtures for failed differential tests when explicitly requested by a debug flag.
- [x] Include the exact Git command, Git version, working directory, and relevant fixture files in differential failure output.

## 14. Suggested Commit Sequence

- [ ] Commit 1: test harness and project cleanup
- [x] Commit 2: path model and normalization
- [x] Commit 3: core glob parser
- [x] Commit 4: core glob matcher
- [x] Commit 5: `.gitignore` parser
- [x] Commit 6: ignore evaluation engine
- [x] Commit 7: Git differential test suite
- [x] Commit 8: repository discovery
- [x] Commit 9: file tree walker
- [x] Commit 10: pruning and traversal diagnostics
- [x] Commit 11: performance tuning
- [x] Commit 12: docs and API polish

## 15. Definition Of Done

- [x] The implementation satisfies the behavioral requirements in [glob_library_specs.md](./glob_library_specs.md).
- [ ] The test suite gives high confidence across unit, differential, integration, cross-platform, and performance coverage.
- [x] Git CLI differential tests are part of the normal validation path, not an occasional manual verification step.
- [x] The public API is documented, coherent, and small enough to remain maintainable.
- [x] The library is ready to be consumed as a single package for standalone globbing, ignore evaluation, and Git-compatible repository traversal.
