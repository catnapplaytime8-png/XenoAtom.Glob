# XenoAtom.Glob User Guide

A high-performance .NET glob library with gitignore compatibility.

## What It Provides

- `GlobPattern` for compiled path matching
- `IgnoreRuleSet` and `IgnoreMatcher` for Git-compatible ignore evaluation
- `IgnoreDialect` to select between `.gitignore` and `.ignore` parsing entry points
- `RepositoryDiscovery` and `RepositoryContext` for Git working tree discovery
- `FileTreeWalker` for ignore-aware traversal

## Glob Matching

```csharp
using XenoAtom.Glob;

var pattern = GlobPattern.Parse("src/**/file.cs");
var matches = pattern.IsMatch("src/nested/file.cs");

ReadOnlySpan<char> spanPath = @"src\nested\file.cs";
var spanMatches = pattern.IsMatch(spanPath);
```

Supported glob features:

- `*`
- `?`
- character classes like `[a-z]` and `[!0-9]`
- escaped literals such as `\*`
- recursive `**`

## Ignore Evaluation

```csharp
using XenoAtom.Glob.Ignore;

var rules = IgnoreRuleSet.ParseGitIgnore("""
    *.tmp
    build/
    !keep.tmp
    """);

var matcher = new IgnoreMatcher(rules);
var result = matcher.Evaluate("build/output.tmp");

ReadOnlySpan<char> spanCandidate = @"build\output.tmp";
var spanResult = matcher.Evaluate(spanCandidate);

using var evaluator = matcher.CreateEvaluator();
var hotPathResult = evaluator.Evaluate("build/output.tmp");
```

Later rule sets passed to `IgnoreMatcher` have higher precedence than earlier ones.

For hot-path callers that already work with spans, the public APIs also accept `ReadOnlySpan<char>` for:

- `GlobPattern.IsMatch(ReadOnlySpan<char>, bool)`
- `IgnoreMatcher.Evaluate(ReadOnlySpan<char>, bool)`
- `IgnoreRuleSet.ParseGitIgnore(ReadOnlySpan<char>, ...)`
- `IgnoreRuleSet.Parse(ReadOnlySpan<char>, IgnoreDialect, ...)`

For repeated ignore checks on hot paths, `IgnoreMatcher.CreateEvaluator()` returns a reusable evaluator that retains pooled normalization and matcher scratch buffers across calls instead of rebuilding them on each evaluation.

For explicit dialect selection:

```csharp
var ignoreRules = IgnoreRuleSet.Parse("""
    *.cache
    """, IgnoreDialect.IgnoreFile);
```

Current dialect notes:

- `IgnoreDialect.GitIgnore` is the Git-compatible path used throughout the repository-aware APIs.
- `IgnoreDialect.IgnoreFile` currently reuses the same parser and evaluator semantics through an explicit adapter.

## Repository Discovery

```csharp
using XenoAtom.Glob.Git;

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo");
Console.WriteLine(repository.WorkingTreeRoot);
Console.WriteLine(repository.GitDirectory);
```

The implementation supports:

- a normal `.git` directory
- a `.git` gitfile that points to another Git directory
- `.git/info/exclude`
- `core.excludesFile`, including quoted values and `~/` expansion
- repository-aware case comparison through `core.ignorecase`

Current repository-aware scope:

- ignore evaluation answers whether a path would be ignored by the ignore engine
- tracked-file state from the Git index is intentionally out of scope for this release
- repository discovery reads the directly available `[core]` values from the discovered config files; `include` or `includeIf` indirection and line continuations are not interpreted yet

## Tree Walking

```csharp
using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo");
var walker = new FileTreeWalker();

foreach (var entry in walker.Enumerate(repository.WorkingTreeRoot, new FileTreeWalkOptions
{
    RepositoryContext = repository,
    IncludeDirectories = false,
}))
{
    Console.WriteLine(entry.RelativePath);
}
```

Traversal characteristics:

- uses `FileSystemEnumerator<T>`
- prunes ignored directories before descent
- loads `.gitignore` lazily when entering directories
- reuses cached parsed ignore files and repository-root ignore state through `RepositoryContext` when file metadata is unchanged
- exposes captured `FileSystemEntry`-style metadata such as `Attributes`, `Length`, and UTC timestamps on `FileTreeEntry`
- does not follow reparse points or symbolic links by default
- supports cancellation through `FileTreeWalkOptions.CancellationToken`
- snapshots the init-only `FileTreeWalkOptions` values and additional rule-set list when enumeration starts

## Thread Safety

- `GlobPattern`, `IgnoreRule`, `IgnoreRuleSet`, `IgnoreMatcher`, `RepositoryContext`, `FileTreeWalkOptions`, `FileTreeEntry`, `GlobPatternParseResult`, and `IgnoreEvaluationResult` are safe to share across threads.
- `RepositoryDiscovery` is stateless and can be called concurrently.
- `FileTreeWalker` instances can be reused concurrently to start separate traversals, but each returned enumeration must be consumed by a single thread at a time.
- `IgnoreMatcherEvaluator` is a mutable hot-path helper. It is not thread-safe, so each concurrent worker should use its own evaluator instance.

## Benchmarks

The repository includes `src/XenoAtom.Glob.Benchmarks/` with BenchmarkDotNet benchmarks for:

- pattern compilation
- single-path glob matching
- ignore evaluation
- traversal with and without ignore pruning
- matching of a pre-collected current-repository file list using the real `.gitignore` stack
- comparison against `LibGit2Sharp.Ignore.IsPathIgnored` on the same pre-collected repository inputs
