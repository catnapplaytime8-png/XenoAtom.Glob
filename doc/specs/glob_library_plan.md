# XenoAtom.Glob Implementation Plan

This plan is the execution checklist for the specification in [glob_library_specs.md](./glob_library_specs.md).

The intent is to give an implementer a concrete sequence of work, explicit quality gates, and a test strategy strong enough to establish high confidence in correctness, performance, and long-term maintainability.

## 1. Delivery Rules

- [ ] Treat [glob_library_specs.md](./glob_library_specs.md) as the behavioral contract.
- [ ] Keep diffs focused and land work in self-contained steps.
- [ ] Add tests before or with each behavior change.
- [ ] Keep `readme.md`, `doc/readme.md`, and the spec docs in sync with shipped behavior.
- [ ] Do not defer correctness gaps behind undocumented TODO behavior.
- [ ] Run the full test suite before closing each milestone.

## 2. Milestone Overview

- [ ] Milestone 0: Development foundation and quality harness
- [ ] Milestone 1: Path model and normalization primitives
- [ ] Milestone 2: Core glob parser and matcher
- [ ] Milestone 3: `.gitignore` parser and rule compilation
- [ ] Milestone 4: Ignore evaluation engine and explanation model
- [ ] Milestone 5: Git-aware repository discovery
- [ ] Milestone 6: File tree walker and traversal pruning
- [ ] Milestone 7: Performance tuning and benchmark hardening
- [ ] Milestone 8: Additional ignore dialect hooks
- [ ] Milestone 9: API review, documentation, and release readiness

## 3. Milestone 0: Development Foundation And Quality Harness

- [ ] Create the initial public API sketch in code comments or stub types so the implementation has a stable target.
- [ ] Establish namespace layout under `XenoAtom.Glob`, `XenoAtom.Glob.Ignore`, `XenoAtom.Glob.IO`, and `XenoAtom.Glob.Git`.
- [ ] Replace placeholder files in the library and tests with meaningful structure.
- [ ] Set up test helper infrastructure for temporary directories, temporary repositories, and path corpus generation.
- [ ] Add a benchmark project or benchmark folder strategy for later BenchmarkDotNet coverage.
- [ ] Add shared test utilities for path normalization, file layout creation, and Git CLI invocation.
- [ ] Add CI expectations for unit tests, Git differential tests, and benchmarks that are safe to run in CI.
- [ ] Require Git to be present in CI on supported platforms and fail fast with a clear message when the differential suite cannot run in environments where it is expected.
- [ ] Add a small test helper that captures and prints `git --version` in differential test runs.

Exit criteria:

- [ ] The repo has a clean test harness and no placeholder production types blocking implementation.
- [ ] Developers can create isolated file-system and repository test fixtures with minimal boilerplate.

## 4. Milestone 1: Path Model And Normalization Primitives

- [ ] Define the internal relative path contract described in [glob_library_specs.md](./glob_library_specs.md).
- [ ] Implement path normalization that converts separators to `/` without using expensive general-purpose path APIs in hot paths.
- [ ] Encode directory/file distinction separately from the path text.
- [ ] Implement validation rules for relative paths used by the matcher and ignore engine.
- [ ] Add low-allocation helpers for slicing segments from normalized paths.
- [ ] Decide and document the internal comparison strategy abstraction needed for case-sensitive versus case-insensitive scenarios.

Tests:

- [ ] Unit tests for separator normalization on Windows-style and Unix-style input.
- [ ] Unit tests for duplicate separators, `.` segments, empty segments, and trailing separator handling.
- [ ] Unit tests for directory flag handling independent of trailing slash input.
- [ ] Unit tests for invalid input rejection behavior.
- [ ] Property-style tests for path normalization invariants on random segment combinations.

Exit criteria:

- [ ] All path consumers can operate on a normalized path representation without calling `Path.GetRelativePath`.
- [ ] Hot-path helpers have allocation behavior measured and documented.

## 5. Milestone 2: Core Glob Parser And Matcher

- [ ] Implement `GlobPattern` parsing for literals, `*`, `?`, character classes, escapes, and `**`.
- [ ] Define a stable representation for compiled tokens and compiled segments.
- [ ] Implement specialized matchers for literal path, literal name, suffix, prefix, single-segment wildcard, and general recursive glob.
- [ ] Add directory-aware matching hooks needed by ignore semantics.
- [ ] Define invalid-pattern behavior and expose it through parse results rather than exceptions in normal control flow.
- [ ] Add a small diagnostic representation for parser debugging that is not used in hot paths.

Tests:

- [ ] Unit tests for exact literal matches and mismatches.
- [ ] Unit tests for `*`, `?`, and character class semantics.
- [ ] Unit tests for escaped wildcard characters and escaped metacharacters.
- [ ] Unit tests for `**/foo`, `foo/**`, `a/**/b`, and adjacent wildcard combinations.
- [ ] Unit tests for segment-boundary behavior where `*` and `?` must not cross `/`.
- [ ] Unit tests for invalid bracket expressions and trailing backslash handling.
- [ ] Property-style tests comparing specialized matcher results against the general matcher on generated cases.
- [ ] Microbenchmarks for compile cost and single-pattern match throughput.

Exit criteria:

- [ ] The glob engine is usable without any ignore-file infrastructure.
- [ ] Specialized and fallback matchers produce identical results for the same semantic input.

## 6. Milestone 3: `.gitignore` Parser And Rule Compilation

- [ ] Implement `.gitignore` line parsing with support for comments, escaped comments, negation, trailing slash, and escaped trailing spaces.
- [ ] Preserve source metadata: source file, source kind, line number, original pattern text.
- [ ] Support LF and CRLF inputs and files without a trailing newline.
- [ ] Compile parsed ignore rules into immutable rule objects using the core glob engine.
- [ ] Capture anchoring mode and directory-only behavior explicitly rather than inferring them at match time.
- [ ] Provide a parser API that can read from text, spans, or streams without forcing the caller into one input model.

Tests:

- [ ] Unit tests for blank lines, comments, escaped `#`, escaped `!`, and escaped spaces.
- [ ] Unit tests for leading slash, middle slash, no slash, and trailing slash semantics at parse level.
- [ ] Unit tests for line numbering and source metadata preservation.
- [ ] Unit tests for invalid lines and invalid escape endings.
- [ ] Corpus tests with curated `.gitignore` snippets covering common and obscure forms.

Exit criteria:

- [ ] Every parsed rule retains enough metadata to explain a winning match later.
- [ ] Parse output is immutable and reusable across many match operations.

## 7. Milestone 4: Ignore Evaluation Engine And Explanation Model

- [ ] Implement ordered rule evaluation with last-match-wins behavior.
- [ ] Implement layered rule sets with precedence matching the spec.
- [ ] Implement per-directory relative evaluation semantics.
- [ ] Implement negation behavior and the no-reinclude-below-pruned-directory rule.
- [ ] Add `IgnoreEvaluationResult` with a minimal fast-path shape and optional expanded diagnostics.
- [ ] Design `IgnoreStack` push/pop mechanics so descending into directories does not rebuild all parent state.

Tests:

- [ ] Unit tests for single-rule include and exclude outcomes.
- [ ] Unit tests for multiple-rule ordering within one file.
- [ ] Unit tests for precedence between parent `.gitignore`, child `.gitignore`, `.git/info/exclude`, and global excludes.
- [ ] Unit tests for directory-only matches versus file matches.
- [ ] Unit tests for basename-only patterns matching at any depth under the rule base.
- [ ] Unit tests for negation and re-inclusion in reachable directories.
- [ ] Unit tests proving that excluded parent directories prevent child re-inclusion from taking effect.
- [ ] Unit tests for explanation payload: winning rule, source file, line number, and raw pattern.

Differential tests:

- [ ] Introduce a Git-backed differential suite using `git check-ignore --no-index -v`.
- [ ] Add batch-oriented differential helpers using `git check-ignore --no-index --stdin -z -v --non-matching`.
- [ ] Compare both the ignore decision and the winning rule metadata against Git.
- [ ] Include cases for `.gitignore`, `.git/info/exclude`, and `core.excludesFile`.
- [ ] Include cases with nested `.gitignore` files and conflicting rules.
- [ ] Include cases with CRLF rule files, escaped spaces, escaped leading `!`, and complex `**` patterns.
- [ ] Include cases that intentionally probe documented Git limitations such as failed re-inclusion below excluded directories.
- [ ] Store fixture definitions in a reusable corpus format so regressions can be reproduced exactly.

Exit criteria:

- [ ] The engine matches Git for the curated compatibility corpus supported by the milestone.
- [ ] Explanation mode reproduces enough metadata to diagnose mismatches against Git.

## 8. Milestone 5: Git-Aware Repository Discovery

- [ ] Implement discovery of a repository root from an arbitrary working-tree path.
- [ ] Support both `.git` directories and `.git` gitfiles containing `gitdir: <path>`.
- [ ] Resolve `.git/info/exclude` from discovered metadata.
- [ ] Resolve the global exclude file from Git configuration or from an explicitly supplied resolved path, depending on the chosen API decision.
- [ ] Make repository discovery explicit and reusable rather than burying it inside traversal.
- [ ] Document any intentionally deferred repository behaviors.

Tests:

- [ ] Unit tests for repository discovery when `.git` is a directory.
- [ ] Unit tests for repository discovery when `.git` is a gitfile with a relative path.
- [ ] Unit tests for worktree-like metadata layouts.
- [ ] Unit tests for submodule-like gitfile layouts.
- [ ] Unit tests for missing or malformed gitfile content.
- [ ] Differential tests proving the discovered root, git dir, and ignore sources match what Git reports for the same temporary repository.
- [ ] Validate repository discovery against `git rev-parse --show-toplevel` and `git rev-parse --git-dir`.

Exit criteria:

- [ ] Repository discovery is correct for normal repositories, linked worktrees, and gitfile-backed working trees in the supported scenarios.

## 9. Milestone 6: File Tree Walker And Traversal Pruning

- [ ] Implement traversal around `FileSystemEnumerable<T>` or the chosen equivalent.
- [ ] Design `FileTreeEntry` so it exposes enough metadata without forcing expensive object creation.
- [ ] Evaluate ignore rules before descending into directories.
- [ ] Load per-directory `.gitignore` lazily when entering a directory.
- [ ] Implement pruning of excluded directories.
- [ ] Keep non-following symlink or reparse-point behavior as the default Git-compatible mode.
- [ ] Add cancellation support to long-running walks.
- [ ] Decide and document whether ordering is unspecified or optionally configurable.

Tests:

- [ ] Unit tests for flat directory enumeration with no ignore rules.
- [ ] Unit tests for nested traversal with parent and child `.gitignore` files.
- [ ] Unit tests proving excluded directories are pruned and not descended into.
- [ ] Unit tests proving reachable directories can still re-include children when allowed.
- [ ] Unit tests for `.gitignore` loading only from directories actually entered.
- [ ] Unit tests for symlink or reparse-point handling where supported by the platform.
- [ ] Integration tests over realistic directory trees with mixed files, directories, and ignore rules.
- [ ] Differential traversal tests that compare the resulting visible path set to a Git-derived expected set in supported scenarios.
- [ ] Differential traversal tests that also compare per-entry ignore decisions against `git check-ignore` for the same fixture paths.

Performance tests:

- [ ] Benchmarks for traversal with no ignore rules.
- [ ] Benchmarks for traversal with shallow ignore stacks.
- [ ] Benchmarks for traversal with deep nested ignore stacks.
- [ ] Benchmarks for traversal where most entries are pruned early.

Exit criteria:

- [ ] Traversal is correct, lazy, and prunes excluded directories before descent.
- [ ] The walk API is usable independently of the Git-specific layer.

## 10. Milestone 7: Performance Tuning And Benchmark Hardening

- [ ] Establish representative benchmark corpora: small, medium, large, deep, wildcard-heavy, and ignore-heavy.
- [ ] Measure allocations and throughput for path normalization, pattern compilation, matching, ignore evaluation, and traversal.
- [ ] Identify high-allocation hot spots with profiler-backed inspection.
- [ ] Introduce pooling or data-structure changes only when measurement justifies them.
- [ ] Verify optimizations do not change semantics by running the full correctness suite after each tuning step.
- [ ] Document expected performance characteristics and known tradeoffs.

Benchmark gates:

- [ ] Benchmark results are reproducible enough to detect regressions.
- [ ] Allocation counts for skipped entries remain near-zero in the intended steady state.
- [ ] No optimization lands without a before-and-after measurement.

## 11. Milestone 8: Additional Ignore Dialect Hooks

- [ ] Extract the parser and rule-evaluation seams needed for additional dialects without weakening `.gitignore` correctness.
- [ ] Add internal or public dialect abstractions only if they stay small and concrete.
- [ ] Prototype the first follow-up dialect support path, likely `.ignore` or `.dockerignore`, behind a focused adapter model.
- [ ] Document exactly what is shared and what remains dialect-specific.

Tests:

- [ ] Unit tests proving `.gitignore` semantics remain unchanged after introducing dialect extension points.
- [ ] Unit tests for any newly introduced dialect adapter behavior.

Exit criteria:

- [ ] `.gitignore` remains the reference-quality path while extension points stay restrained.

## 12. Milestone 9: API Review, Documentation, And Release Readiness

- [ ] Review public API naming, overload shape, XML docs, and discoverability.
- [ ] Remove placeholder or experimental types that should not ship.
- [ ] Add XML docs for all public APIs, including exceptions and behavioral caveats.
- [ ] Update `readme.md` with the final capability set and usage examples.
- [ ] Update `doc/readme.md` with a more detailed guide for globbing, ignore evaluation, and traversal.
- [ ] Cross-check this plan and the spec against the actual implementation and mark deferred items explicitly.
- [ ] Add examples for standalone glob use, standalone ignore use, and repository traversal use.
- [ ] Review AOT and trimming compatibility warnings and resolve or document them.

Release gate:

- [ ] All tests pass locally and in CI.
- [ ] Differential compatibility suite is green.
- [ ] Benchmark results are recorded for the release candidate.
- [ ] Docs match the shipped API and behavior.

## 13. Top-Confidence Test Plan

This section is the quality backbone for the project and should be treated as mandatory work, not optional hardening.

### 13.1 Unit Test Matrix

- [ ] Path normalization tests
- [ ] Glob parser tests
- [ ] Glob matcher tests
- [ ] `.gitignore` parser tests
- [ ] Ignore rule precedence tests
- [ ] Negation and pruning tests
- [ ] Repository discovery tests
- [ ] Traversal tests
- [ ] Public API guard tests for argument validation and XML-doc-covered behavior

### 13.2 Differential Compatibility Matrix

- [ ] Compare ignore decisions to `git check-ignore --no-index`.
- [ ] Compare winning source file, line number, and pattern text to Git verbose output.
- [ ] Compare batch query behavior using `git check-ignore --no-index --stdin -z -v --non-matching`.
- [ ] Cover nested `.gitignore` files with overrides.
- [ ] Cover `.git/info/exclude`.
- [ ] Cover `core.excludesFile`.
- [ ] Cover worktree and gitfile repository layouts.
- [ ] Cover line ending variants and escaped-space cases.
- [ ] Cover case-sensitivity behavior on each supported platform.
- [ ] Capture the Git version in test output and in failure diagnostics.
- [ ] Fail the compatibility gate if Git and the library disagree on a supported scenario.

### 13.3 Cross-Platform Matrix

- [ ] Run the full suite on Windows.
- [ ] Run the full suite on Linux.
- [ ] Run the full suite on macOS.
- [ ] Ensure platform-conditional expectations are explicit and localized in the tests.
- [ ] Add targeted tests for separator handling and platform-specific path edge cases.

### 13.4 Fuzz And Property Coverage

- [ ] Add property-style tests for normalization and matcher invariants.
- [ ] Add parser fuzz-style inputs for malformed bracket expressions, dangling escapes, unusual whitespace, and repeated separators.
- [ ] Add generated corpus tests that compare specialized matchers with the fallback matcher.
- [ ] Add generated ignore corpora for rule-order and negation invariants.

### 13.5 Realistic Integration Corpora

- [ ] Build reusable file-tree fixtures that look like real repositories with nested source, build, temp, package, and tool output directories.
- [ ] Add deep-tree fixtures to exercise pruning and stack behavior.
- [ ] Add fixtures with large sibling counts to stress enumeration.
- [ ] Add fixtures with many small `.gitignore` files to stress rule-stack updates.

### 13.6 Performance Regression Coverage

- [ ] Benchmark pattern compilation throughput.
- [ ] Benchmark single-path match throughput.
- [ ] Benchmark ignore evaluation throughput with 1, 10, 100, and 1000 effective rules.
- [ ] Benchmark traversal throughput on small, medium, and large trees.
- [ ] Track allocations for hot scenarios.
- [ ] Add regression thresholds or recorded baselines where CI noise permits.

### 13.7 Failure Analysis Tooling

- [ ] Ensure test failures print enough diagnostics to compare the library result and Git result quickly.
- [ ] Provide helper assertions that print normalized path, directory flag, winning rule metadata, and evaluated rule stack.
- [ ] Preserve temporary fixtures for failed differential tests when explicitly requested by a debug flag.
- [ ] Include the exact Git command, Git version, working directory, and relevant fixture files in differential failure output.

## 14. Suggested Commit Sequence

- [ ] Commit 1: test harness and project cleanup
- [ ] Commit 2: path model and normalization
- [ ] Commit 3: core glob parser
- [ ] Commit 4: core glob matcher
- [ ] Commit 5: `.gitignore` parser
- [ ] Commit 6: ignore evaluation engine
- [ ] Commit 7: Git differential test suite
- [ ] Commit 8: repository discovery
- [ ] Commit 9: file tree walker
- [ ] Commit 10: pruning and traversal diagnostics
- [ ] Commit 11: performance tuning
- [ ] Commit 12: docs and API polish

## 15. Definition Of Done

- [ ] The implementation satisfies the behavioral requirements in [glob_library_specs.md](./glob_library_specs.md).
- [ ] The test suite gives high confidence across unit, differential, integration, cross-platform, and performance coverage.
- [ ] Git CLI differential tests are part of the normal validation path, not an occasional manual verification step.
- [ ] The public API is documented, coherent, and small enough to remain maintainable.
- [ ] The library is ready to be consumed as a single package for standalone globbing, ignore evaluation, and Git-compatible repository traversal.
