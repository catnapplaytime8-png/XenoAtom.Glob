# XenoAtom.Glob Library Specification

Related documents:

- Implementation plan: [glob_library_plan.md](./glob_library_plan.md)

## 1. Purpose

`XenoAtom.Glob` will be a single high-performance .NET library for:

- Compiling and matching glob patterns against file and directory paths.
- Resolving layered ignore rules, with first-class `.gitignore` compatibility.
- Walking directory trees efficiently while applying include and ignore rules during traversal.
- Exposing reusable building blocks so callers can use only pattern matching, only ignore evaluation, only traversal, or the full stack.

The library is intended for repository-scale file discovery and filtering, but its architecture must remain generic and not hard-code Git as the only domain model.

## 2. Core Product Goals

- Provide exact `.gitignore` behavior for working-tree enumeration and ignore resolution.
- Keep the implementation allocation-conscious and suitable for large trees and hot loops.
- Offer a single package with cohesive APIs, while keeping the internal and public model componentized.
- Make the fast path obvious in the API and in the implementation.
- Support Windows, Linux, and macOS behavior without leaking platform-specific path quirks into the public surface.
- Remain AOT-friendly, trimming-friendly, and free from reflection-based design.

## 3. Non-Goals

- Re-implement the Git index, object database, or full pathspec engine.
- Depend on external native code.
- Expose an overly generic plugin system before there is a concrete need.
- Guarantee output ordering by default if doing so would reduce traversal throughput.
- Model every ignore dialect as if they were identical to `.gitignore`.

## 4. Compatibility Baseline

The initial compatibility target is the behavior documented by the official Git manuals current on April 3, 2026, and validated locally against `git version 2.53.0.windows.2`.

Primary references:

- `gitignore`
- `git-check-ignore`
- `gitrepository-layout`
- `git-config`

Where the documentation is insufficiently explicit, behavior must be verified by differential tests against the installed Git CLI and treated as the compatibility source of truth for that case.

For supported Git-compatible scenarios, the installed Git CLI is the normative execution oracle for tests. A discrepancy between the library and Git must be treated as a correctness bug unless the scenario is explicitly documented as out of scope.

## 5. Design Principles

### 5.1 Single library, multiple layers

The package ships as one assembly, but with a small set of composable subsystems:

- Glob pattern compilation and matching
- Ignore file parsing
- Ignore rule evaluation
- Repository-aware ignore source discovery
- File tree traversal

Each subsystem must be usable independently.

### 5.2 Performance is a feature

Performance requirements are part of the design contract:

- Avoid regex.
- Avoid `string.Split`, `Path.GetRelativePath`, and repeated path normalization in hot paths.
- Avoid allocating `FileInfo` and `DirectoryInfo` during traversal.
- Prune excluded directories before descending into them.
- Separate cold parsing/compilation from hot matching/evaluation.
- Favor `ReadOnlySpan<char>`, `Span<T>`, pooled buffers, and immutable compiled rule tables.

### 5.3 Correctness over cleverness

The library must not sacrifice Git compatibility for a cleaner but incompatible model. If Git behavior is surprising, the implementation and docs must preserve the behavior and explain it.

### 5.4 Explicit path model

All hot-path matching operates on normalized relative paths using `/` as the internal separator. Platform-specific path parsing is done once at the boundary.

## 6. High-Level Architecture

The library is organized around six core concepts.

### 6.1 `GlobPattern`

Represents one compiled glob expression independent of any ignore file semantics.

Responsibilities:

- Parse the input pattern.
- Preserve compilation metadata.
- Classify the pattern for fast matching.
- Match against a normalized path or path segment sequence.

### 6.2 `IgnoreRule`

Represents one parsed ignore rule with source metadata.

State includes:

- Raw pattern text
- Compiled glob form
- Negation flag
- Directory-only flag
- Anchoring mode
- Line number
- Source kind
- Source path
- Base directory relative to the evaluation root

### 6.3 `IgnoreRuleSet`

An immutable ordered set of ignore rules from a single source or logical source layer.

Responsibilities:

- Preserve rule order.
- Evaluate the last matching rule.
- Return both decision and explanation metadata when requested.

### 6.4 `IgnoreStack`

Represents the effective ignore state at a specific traversal depth.

Responsibilities:

- Carry inherited rule layers from parent directories.
- Add the current directory's local ignore file rules.
- Evaluate files and directories relative to the traversal root.
- Support stack push/pop without rebuilding the world.

### 6.5 `FileTreeWalker`

Performs directory traversal with filtering.

Responsibilities:

- Enumerate entries with minimal metadata cost.
- Consult ignore and include logic before descent.
- Offer a low-allocation streaming API.
- Avoid unnecessary path materialization.

### 6.6 `RepositoryContext`

Optional Git-aware discovery state used only when callers ask for Git-compatible behavior.

Responsibilities:

- Discover repository root.
- Resolve `.git` directory versus gitfile indirection.
- Resolve `.git/info/exclude`.
- Resolve `core.excludesFile`.
- Resolve repository-aware case comparison from `core.ignorecase` when present.
- Carry repository-scoped compatibility options.

## 7. Public API Direction

The public API should stay small. The implementation may have more internal types, but the external mental model should remain simple.

Proposed primary types:

- `GlobPattern`
- `GlobMatcher`
- `IgnoreDialect`
- `IgnoreRule`
- `IgnoreRuleSet`
- `IgnoreMatcher`
- `IgnoreEvaluationResult`
- `IgnoreOptions`
- `GitIgnoreOptions`
- `FileTreeWalker`
- `FileTreeWalkOptions`
- `FileTreeEntry`
- `RepositoryContext`
- `RepositoryDiscovery`

Proposed top-level usage paths:

1. Compile and match a single glob.
2. Parse and evaluate ignore rules against relative paths.
3. Walk a directory tree with ignore handling.
4. Discover a repository context and run Git-compatible traversal.

The API should favor static factory methods and `Try*` parsing methods over large constructor surfaces.

For layered ignore evaluation, the public surface may expose an explicit matcher type that receives ordered rule sets and treats later rule sets as higher precedence than earlier ones.

## 8. Path Model

### 8.1 Normalized path representation

The internal matcher operates on:

- Relative paths only
- `/` as separator
- No `.` segments
- No duplicate separators
- No trailing separator unless the caller is explicitly marking a directory in a non-hot diagnostic path

Directory-ness must be carried as a separate boolean, not encoded only by a trailing slash.

### 8.2 Platform handling

Boundary normalization must:

- Accept `\` and `/` as input separators on Windows.
- Preserve case by default.
- Avoid full-path canonicalization in the hot path.
- Avoid resolving symlinks during normal matching.

Git-aware mode must keep case-sensitivity as an explicit compatibility concern. The exact default comparison policy must be locked down by differential tests on each supported platform instead of being guessed from API preference alone.

The implementation may use separate comparison defaults for standalone glob matching and Git-compatible ignore evaluation, as long as the behavior is explicit and covered by differential tests.

### 8.3 Relative path discipline

All ignore evaluation is relative to an explicit evaluation root and, for per-directory ignore files, relative to the directory containing the ignore file, matching Git's documented behavior.

## 9. Glob Semantics

The reusable glob engine must support a strict core glob model and an ignore-oriented mode.

### 9.1 Core tokens

Support:

- Literal characters
- `*`
- `?`
- Character classes like `[abc]`, `[a-z]`, `[!a-z]`
- Escaped literals with `\`
- Path separator awareness
- Recursive directory wildcard `**`

### 9.2 Slash handling

The matcher must distinguish:

- Segment-local wildcards that cannot cross `/`
- Recursive wildcards that can cross zero or more segments

### 9.3 Invalid forms

The parser must define a stable policy for invalid patterns.

For `.gitignore` compatibility:

- A trailing backslash produces an invalid rule that never matches.
- Escapes must preserve the following character literally.
- Consecutive `**` forms must follow Git behavior rather than an invented stricter grammar.

### 9.4 Classification for fast paths

Each compiled pattern must be classified into a specialized matcher shape where possible:

- Exact literal name
- Exact literal path
- Prefix path
- Suffix extension
- Single-segment wildcard
- Directory subtree include or exclude
- General recursive glob

The matcher dispatch should hit specialized code before falling back to the general engine.

## 10. `.gitignore` Semantics

The library must implement Git's ignore behavior as documented and verified.

### 10.1 Ignore sources and precedence

From highest to lowest precedence:

1. Command-provided patterns, if the caller uses that feature.
2. Per-directory `.gitignore` files from the path's directory up through parent directories, with lower directories overriding higher ones.
3. `.git/info/exclude`.
4. The file referenced by `core.excludesFile`.

Within a single source level, the last matching rule wins.

### 10.2 Rule syntax

A `.gitignore` parser must support:

- Blank lines as no-op separators.
- `#` comments unless the `#` is escaped.
- Trailing spaces ignored unless escaped.
- Leading `!` for negation unless escaped.
- `/` as directory separator.
- Leading slash semantics.
- Middle slash semantics.
- Trailing slash for directory-only rules.
- `*`, `?`, character classes, and `**`.
- Escaped literal forms such as `\#`, `\!`, `\ `, and `\*`.
- LF and CRLF line endings, with or without a final newline.

### 10.3 Relative matching rules

Matching must follow Git's documented rules:

- A pattern with a slash at the beginning or in the middle is relative to the directory containing the ignore file.
- A pattern without a slash may match at any depth below the directory containing the ignore file.
- A pattern with a trailing slash matches directories only.

### 10.4 Negation and re-inclusion

Negation must follow Git's important limitation:

- A negated rule can re-include a path only if an ancestor directory was not already excluded from traversal.
- If a parent directory is excluded and therefore never descended into, patterns for children have no effect.

This pruning constraint is mandatory for both correctness and performance.

### 10.5 Recursive wildcard behavior

`.gitignore`-mode matching must preserve Git's documented `**` behavior:

- `**/foo` matches `foo` at any depth.
- `abc/**` matches everything inside `abc` with infinite depth.
- `a/**/b` matches zero or more intermediate directories.
- Other consecutive asterisks must follow Git's actual behavior and differential tests.

### 10.6 Case sensitivity

Repository-aware ignore evaluation must honor Git's configured case-comparison behavior:

- If `core.ignorecase` is configured for the repository, the matcher must use it.
- If it is not configured, the implementation may fall back to the platform default comparison strategy.
- Differential tests must cover both case-sensitive and case-insensitive repository configurations where the platform and Git installation permit them.

### 10.7 Tracked files

Ignore rules do not apply to already tracked files in Git. The library itself will not implement index tracking, but the Git integration layer must make this boundary explicit:

- Pure ignore evaluation answers "would this path be ignored by the ignore engine?"
- Repository-aware higher-level APIs may later add optional tracked-file integration, but it is out of scope for the first iteration.

### 10.8 Symlink handling for ignore files

The Git compatibility layer must not follow a symlink when reading a working-tree `.gitignore` file, matching the Git documentation.

## 11. Git-Aware Repository Discovery

Git compatibility requires more than parsing `.gitignore`.

### 11.1 Repository root detection

The Git integration layer must support:

- A `.git` directory at the working tree root.
- A `.git` text file containing `gitdir: <path>`, as used by worktrees and submodules.

### 11.2 Repository-scoped ignore sources

The Git integration layer must resolve:

- Per-directory `.gitignore`
- `.git/info/exclude`
- `core.excludesFile`

Resolution must be based on the discovered repository context, not on ad hoc file lookups.

### 11.3 Worktrees and submodules

The repository discovery model must be compatible with:

- Linked worktrees
- Submodules using a gitfile

The first implementation does not need to inspect Git objects, but it must correctly find the repository metadata and ignore files for these working tree layouts.

## 12. Traversal Model

### 12.1 Enumerator strategy

Directory walking should be implemented around `FileSystemEnumerable<T>` or an equivalent zero- or low-allocation primitive available in the target framework.

The walker must:

- Avoid stat calls not needed for the active options.
- Read just enough metadata to distinguish file, directory, and reparse-point behavior.
- Keep the transform callback cheap.
- Minimize `string` creation for skipped entries.

### 12.2 Traversal behavior

The default fast traversal mode:

- Streams entries as they are found.
- Does not guarantee ordering.
- Evaluates ignore rules before descending into child directories.
- Loads `.gitignore` only when entering a directory that may contain one.

### 12.3 Directory pruning

When a directory is excluded:

- The walker should prune it immediately.
- No descendant paths under it should be evaluated unless an explicit option asks for exhaustive diagnostics.

### 12.4 Symlinks and reparse points

The first implementation must expose a clear option for how to treat symbolic links and directory reparse points:

- Do not follow by default.
- Allow caller-controlled follow behavior later if required.
- Keep Git-compatible traversal conservative by default.

### 12.5 Cancellation and backpressure

Traversal APIs should support cancellation without forcing `async`:

- Synchronous `CancellationToken` checks are sufficient in the first version.
- Enumeration should remain lazy and streaming.

## 13. Performance Requirements

### 13.1 Allocation targets

The design target is amortized near-zero allocations per skipped entry in steady-state traversal, aside from unavoidable allocations from the underlying runtime enumeration APIs.

The implementation should:

- Pool temporary buffers.
- Avoid allocating relative path strings for entries rejected before emission.
- Cache parsed ignore files by full path and last-write metadata when such caching is enabled.
- Reuse traversal stack frames where practical.

### 13.2 Hot path constraints

The hot path must avoid:

- Regex engines
- LINQ
- Boxing
- Exception-driven control flow
- Per-entry path normalization through general-purpose path APIs

### 13.3 Compiled matching

Pattern compilation must happen once, with the hot matcher using:

- Pre-tokenized segments
- Precomputed literal spans where possible
- Specialized matcher branches
- Branch-light directory pruning logic

### 13.4 Diagnostics without hot-path tax

Verbose explanation mode must be opt-in.

The default matcher should return only the minimum data needed for traversal decisions. Line number, source file, and winning rule text should be materialized only when requested.

## 14. Reusability Requirements

The library must support these usage modes without forcing the caller through Git-specific APIs.

### 14.1 Standalone glob matching

Example use cases:

- Filter a known set of relative paths.
- Match file names in memory.
- Build custom inclusion logic.

### 14.2 Standalone ignore evaluation

Example use cases:

- Parse one ignore file and test paths against it.
- Build a custom ignore layer for an editor or build tool.
- Apply `.ignore`, `.dockerignore`, or another dialect through an alternate parser while reusing the evaluation engine where semantics overlap.

### 14.3 Full traversal

Example use cases:

- Search a working tree.
- Enumerate candidate files for build or tooling pipelines.
- Power repository analysis tools.

## 15. Other Ignore Dialects

The library should be designed so `.gitignore` is a first-class dialect, and other dialects can reuse shared components without pretending to be Git.

### 15.1 Shared pieces

Reusable pieces for other dialects:

- Pattern compilation
- Rule ordering
- Include or exclude evaluation
- File tree traversal

### 15.2 Dialect-specific pieces

Dialect-specific pieces:

- Parsing rules
- Default precedence
- Anchoring rules
- Escape rules
- Special-case semantics

### 15.3 First follow-up dialects

The design should leave room for:

- `.ignore`
- `.dockerignore`

Support for these dialects should come through explicit dialect adapters, not by weakening `.gitignore` correctness.

The first follow-up dialect may intentionally be `.ignore`, provided it is exposed as an explicit dialect selection and documented independently from `.gitignore`.

## 16. Diagnostics and Introspection

The library should provide optional introspection to support debugging and tests.

Capabilities should include:

- Whether a path matched
- Whether it was ignored or included
- Which rule won
- Rule source file
- Rule source line number
- Rule text as written

This should mirror the kind of information available from `git check-ignore -v`, while remaining library-friendly.

## 17. Testing Strategy

Correctness must be proved with a mix of focused unit tests and differential tests.

### 17.1 Parser tests

Test:

- Comments and escaped comments
- Escaped leading `!`
- Escaped trailing spaces
- Invalid trailing backslash
- Character classes
- `**` combinations
- Leading, middle, and trailing slash semantics

### 17.2 Matcher tests

Test:

- Literal, wildcard, and recursive wildcard paths
- Directory-only patterns
- Basename-only patterns
- Relative-to-ignore-file path anchoring
- Windows separator normalization

### 17.3 Differential Git tests

Add a Git-backed test corpus that:

- Creates temporary repositories.
- Writes `.gitignore`, `.git/info/exclude`, and optional global exclude files.
- Queries paths with `git check-ignore --no-index -v`.
- Compares the library's decision and winning rule metadata to Git's output.

This differential corpus is mandatory for edge cases where documentation alone is ambiguous.

This differential suite should be treated as a first-class compatibility gate, not as an optional integration test layer.

The preferred Git CLI probes are:

- `git check-ignore --no-index -v` for path-level ignore decisions and winning rule metadata
- `git check-ignore --no-index --stdin -z -v --non-matching` for high-volume batch differential cases
- `git rev-parse --show-toplevel` and `git rev-parse --git-dir` for repository discovery validation
- `git config --path core.excludesFile` when validating global exclude resolution behavior

For traversal-oriented validation, the preferred approach is:

- Compare per-entry ignore decisions against `git check-ignore`
- Add fixture-level set comparisons using Git commands only where the command semantics match the library scenario exactly

The suite must record the Git version used for the run so behavior changes across Git releases can be diagnosed precisely.

### 17.4 Traversal tests

Test:

- Directory pruning behavior
- Re-inclusion limitations
- Repository root discovery using `.git` directory and gitfile
- Worktree-compatible metadata layouts
- Symlinked `.gitignore` non-follow behavior where the platform permits the scenario
- Differential traversal fixtures that validate library decisions against Git on the same repository layout

### 17.5 Performance benchmarks

Benchmark separately:

- Pattern compilation
- Single-path matching
- Ignore evaluation with shallow and deep rule stacks
- Tree traversal on small, medium, and large directory corpora
- Traversal scenarios where ignored root entries are skipped before path materialization
- Worst-case wildcard-heavy rule sets

Use BenchmarkDotNet and keep representative real-world corpora in a reproducible test asset strategy.

## 18. Implementation Phases

Implementation should proceed in focused stages.

### Phase 1

- Path normalization primitives
- Core glob parser and matcher
- Unit tests for pure glob behavior

### Phase 2

- `.gitignore` parser
- Compiled ignore rules
- Ignore evaluation engine
- Differential tests using `git check-ignore`

### Phase 3

- File tree walker
- Directory pruning
- Low-allocation traversal API
- Traversal tests and benchmarks

### Phase 4

- Repository discovery
- `.git/info/exclude`
- `core.excludesFile`
- Worktree and submodule gitfile support

### Phase 5

- Additional ignore dialect adapters
- Caching refinements
- Documentation and examples

## 19. Acceptance Criteria

The first production-quality release of this library should satisfy all of the following:

- Exact `.gitignore` rule parsing and matching for the tested corpus.
- Differential parity with Git for the supported ignore scenarios.
- A mandatory Git CLI differential suite that passes on supported CI platforms.
- Traversal that prunes excluded directories before descending.
- Public APIs for standalone glob matching, ignore evaluation, and file tree walking.
- No dependency on regex or reflection-based infrastructure.
- Documentation describing supported behavior and any explicitly deferred cases.
- Benchmark evidence showing low-allocation steady-state traversal and competitive matching throughput.

## 20. Open Decisions To Resolve During Implementation

These decisions should be finalized early and then documented in the user guide:

- Whether the public API exposes `ref struct`-based hot-path matchers or keeps them internal.
- Whether global ignore resolution should read Git config directly or accept an externally supplied resolved path in the first version.
- What the exact default case-sensitivity policy is in Git-aware mode on each supported platform, as proven by differential tests.
- Whether cached ignore files should be keyed only by path or by path plus file timestamp or length.
- Whether traversal APIs should expose file system entry handles directly or map them into a stable library-owned entry type.
- Whether explicit inclusion patterns should be part of the first traversal API or follow after the ignore core is stable.

## 21. Recommended Initial Namespace Layout

One package, with a restrained namespace split:

- `XenoAtom.Glob`
- `XenoAtom.Glob.Ignore`
- `XenoAtom.Glob.IO`
- `XenoAtom.Glob.Git`

If the implementation stays small enough, collapsing these into fewer namespaces is preferable to adding ceremonial abstractions.

## 22. Documentation Requirements

When the implementation starts landing, the following docs must stay in sync:

- `readme.md`
- `doc/readme.md`
- `doc/specs/glob_library_specs.md`
- `doc/specs/glob_library_plan.md`

The readme should stay outcome-focused. This specification is the detailed design reference.

## 23. Reference Notes

Behavioral references used for this specification:

- Git `gitignore` manual: https://git-scm.com/docs/gitignore
- Git `git-check-ignore` manual: https://git-scm.com/docs/git-check-ignore
- Git `gitrepository-layout` manual: https://git-scm.com/docs/gitrepository-layout
- Git `git-config` manual: https://git-scm.com/docs/git-config

These references define the documented baseline. Differential tests against the installed Git CLI remain the final arbiter for ambiguous edge cases.
