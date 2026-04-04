# XenoAtom.Glob User Guide

A high-performance .NET glob library with gitignore compatibility.

This guide is organized by the way people usually approach the library:

1. match paths with a glob
2. evaluate ignore rules
3. work with a real Git repository
4. walk the file system

## Choose The Right API

Use `GlobPattern` when you want to answer "does this relative path match this glob?"

Use `IgnoreRuleSet` plus `IgnoreMatcher` when you want `.gitignore`-style ignore decisions but already have the ignore file content.

Use `RepositoryDiscovery` plus `FileTreeWalker` when you want to traverse a real working tree and let the library load `.gitignore`, `.git/info/exclude`, and `core.excludesFile` for you.

## Path Input Rules

These rules are important because they apply across the library:

- Paths are relative, not absolute.
- `..` segments are not supported.
- Both `/` and `\` are accepted on input.
- Internal normalization uses `/`.
- `FileTreeEntry.RelativePath` always uses `/`.
- If you are matching or evaluating a directory directly, pass `isDirectory: true`.

Examples:

```csharp
var pattern = GlobPattern.Parse("src/**/file.cs");

pattern.IsMatch("src/nested/file.cs");    // true
pattern.IsMatch(@"src\nested\file.cs");   // true

var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("build/\n"));

matcher.Evaluate("build", isDirectory: true).IsIgnored; // true
matcher.Evaluate("build").IsIgnored;                    // false
```

## Glob Matching

`GlobPattern` is the lowest-level API. It parses once, then reuses the compiled pattern.

```csharp
using XenoAtom.Glob;

var pattern = GlobPattern.Parse("src/**/[Tt]est?.cs");

pattern.IsMatch("src/unit/Test1.cs");     // true
pattern.IsMatch("src/unit/test2.cs");     // true
pattern.IsMatch("src/unit/test20.cs");    // false
```

Supported syntax:

- `*` matches within a single path segment
- `?` matches a single character within a segment
- character classes such as `[a-z]` and `[!0-9]`
- escaped literals such as `\*`, `\?`, `\#`, `\!`
- `**` matches across path segments

Important behavior:

- `GlobPattern` matches relative paths only.
- `GlobPattern` is case-sensitive.
- Pattern text cannot start or end with a path separator.
- Directory matching is explicit through `isDirectory: true`.

Examples:

```csharp
GlobPattern.Parse("src/*.cs").IsMatch("src/app.cs");        // true
GlobPattern.Parse("src/*.cs").IsMatch("src/lib/app.cs");    // false

GlobPattern.Parse("src/**/app.cs").IsMatch("src/app.cs");       // true
GlobPattern.Parse("src/**/app.cs").IsMatch("src/lib/app.cs");   // true

GlobPattern.Parse(@"data/\*.txt").IsMatch("data/*.txt");    // true
```

If you want a non-throwing parse path, use `GlobPattern.TryParse`.

## Ignore Evaluation

Use `IgnoreRuleSet.ParseGitIgnore` to parse Git-compatible ignore content, then `IgnoreMatcher` to evaluate paths.

```csharp
using XenoAtom.Glob.Ignore;

var ruleSet = IgnoreRuleSet.ParseGitIgnore("""
    *.tmp
    build/
    !build/keep.tmp
    src/generated/*
    !src/generated/include.txt
    """);

var matcher = new IgnoreMatcher(ruleSet);

matcher.Evaluate("file.tmp").IsIgnored;                  // true
matcher.Evaluate("build/output.bin").IsIgnored;          // true
matcher.Evaluate("build/keep.tmp").IsIgnored;            // true
matcher.Evaluate("src/generated/code.cs").IsIgnored;     // true
matcher.Evaluate("src/generated/include.txt").IsIgnored; // false
```

### How Ignore Decisions Work

- Rules are evaluated in order.
- The last matching rule wins.
- A negated rule starts with `!`.
- A directory ignored by an ancestor rule still blocks deeper children unless the directory itself is made reachable again.

That last point is straight Git behavior and is the most common surprise.

Example:

```csharp
var blocked = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
    build/
    !build/keep.tmp
    """));

blocked.Evaluate("build/keep.tmp").IsIgnored; // true

var reachable = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
    build/*
    !build/keep.tmp
    """));

reachable.Evaluate("build/keep.tmp").IsIgnored; // false
```

### Common `.gitignore` Rule Forms

```csharp
var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
    *.log
    bin/
    foo/*
    foo/**
    !foo/keep.txt
    /root-only.txt
    docs/**/*.bak
    """));
```

What these mean:

- `*.log`: ignore matching basenames anywhere
- `bin/`: ignore directories named `bin` and everything under them
- `foo/*`: ignore immediate children under `foo`, but not `foo` itself
- `foo/**`: ignore descendants under `foo`, but not `foo` itself
- `!foo/keep.txt`: re-include that path if all ancestors are reachable
- `/root-only.txt`: anchored to the rule set's base directory
- `docs/**/*.bak`: ignore matching files under `docs` at any depth

### Layering Multiple Rule Sets

`IgnoreMatcher` accepts multiple `IgnoreRuleSet` instances. Later rule sets have higher precedence.

```csharp
var rootRules = IgnoreRuleSet.ParseGitIgnore("*.tmp");
var nestedRules = IgnoreRuleSet.ParseGitIgnore("!keep.tmp", baseDirectory: "src");

var matcher = new IgnoreMatcher(rootRules, nestedRules);

matcher.Evaluate("src/keep.tmp").IsIgnored;   // false
matcher.Evaluate("other/keep.tmp").IsIgnored; // true
```

Use `baseDirectory` when a rule set comes from a nested directory such as `src/.gitignore`.

### `.gitignore` And `.ignore`

If you want Git-compatible semantics, use `IgnoreRuleSet.ParseGitIgnore`.

If you want explicit dialect selection, use `IgnoreRuleSet.Parse`:

```csharp
var ignoreRules = IgnoreRuleSet.Parse("""
    *.cache
    artifacts/
    """, IgnoreDialect.IgnoreFile);
```

At the moment, `.ignore` parsing uses the same semantics as the Git-compatible parser through an explicit dialect entry point.

### Path Separators And Ignore Rules

Ignore rule syntax itself uses `/`, like Git.

Candidate paths can use either separator:

```csharp
var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("src/generated/\n"));

matcher.Evaluate("src/generated/file.cs").IsIgnored;   // true
matcher.Evaluate(@"src\generated\file.cs").IsIgnored;  // true
```

### Case Sensitivity For Ignore Matching

Standalone `IgnoreMatcher` uses the current platform default path comparison.

Repository-aware operations use the repository's `core.ignorecase` value when available.

That means:

- `GlobPattern` behavior is always case-sensitive.
- standalone ignore behavior follows the current platform default
- repository-aware ignore behavior follows the discovered repository configuration when possible

### Reusing An Evaluator On Hot Paths

If you are evaluating many paths repeatedly, use `IgnoreMatcher.CreateEvaluator()`.

```csharp
var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
    *.tmp
    vendor/
    """));

using var evaluator = matcher.CreateEvaluator();

foreach (var path in paths)
{
    if (evaluator.Evaluate(path).IsIgnored)
    {
        // skip
    }
}
```

This keeps pooled scratch buffers across calls and avoids rebuilding temporary state each time.

## Working With A Git Repository

Use `RepositoryDiscovery` when you have a path inside a working tree and want repository-aware behavior.

```csharp
using XenoAtom.Glob.Git;

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo\src");

Console.WriteLine(repository.WorkingTreeRoot);
Console.WriteLine(repository.GitDirectory);
Console.WriteLine(repository.InfoExcludePath);
Console.WriteLine(repository.GlobalExcludePath);
```

Repository discovery supports:

- a normal `.git` directory
- a `.git` gitfile
- `.git/info/exclude`
- `core.excludesFile`
- `core.ignorecase`

What repository-aware mode means in this library:

- it evaluates ignore rules from the repository root down to the current location
- it honors repository-level and global exclude files
- it uses repository case-comparison rules when available
- it treats nested checked-out repositories and submodules as traversal boundaries

What it intentionally does not do:

- it does not consult the Git index to decide whether a tracked file should be shown despite ignore rules

That is deliberate. This library models ignore behavior, not index state.

## Walking The File System

`FileTreeWalker` enumerates files and optionally directories.

```csharp
using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo");
var walker = new FileTreeWalker();

foreach (var entry in walker.Enumerate(repository.WorkingTreeRoot, new FileTreeWalkOptions
{
    RepositoryContext = repository,
    IncludeDirectories = true,
}))
{
    Console.WriteLine($"{entry.RelativePath} | dir={entry.IsDirectory}");
}
```

### What `FileTreeWalker` Does

- traverses lazily
- prunes ignored directories before descending into them
- loads `.gitignore` files lazily as directories are entered
- reuses ignore caches through `RepositoryContext`
- returns normalized relative paths
- captures metadata such as `Attributes`, `Length`, and UTC timestamps

### Important Traversal Behavior

- `IncludeDirectories = false` yields only files.
- `IncludeDirectories = true` yields directories too.
- `FollowSymbolicLinks = false` by default.
- nested Git working trees and checked-out submodules are treated as boundaries when a `RepositoryContext` is supplied
- each traversal can be cancelled with `CancellationToken`

### Extra Rule Sets During Traversal

You can add more ignore rule sets on top of repository rules:

```csharp
var extraRules = IgnoreRuleSet.ParseGitIgnore("""
    *.cache
    artifacts/
    """);

var entries = walker.Enumerate(rootPath, new FileTreeWalkOptions
{
    RepositoryContext = repository,
    AdditionalRuleSets = [extraRules],
});
```

Later additional rule sets have higher precedence than earlier ones.

## Quick Reference

### I have path strings and a glob

Use `GlobPattern`.

### I have `.gitignore` text and want ignore answers

Use `IgnoreRuleSet.ParseGitIgnore` plus `IgnoreMatcher`.

### I need Git-aware ignore behavior from disk

Use `RepositoryDiscovery` and `FileTreeWalker`.

### Can I pass Windows backslashes?

Yes. Input paths can use `\` or `/`.

### Are output paths normalized?

Yes. Returned relative paths use `/`.

### Can I pass an absolute path to `GlobPattern` or `IgnoreMatcher`?

No. Use relative paths.

### Do I need to tell the matcher when something is a directory?

Yes, if you are evaluating that directory directly. Traversal APIs already know.

### Does this behave exactly like `git status`?

No. Ignore semantics are Git-compatible, but tracked-file/index behavior is intentionally out of scope.

## Thread Safety

- `GlobPattern`, `IgnoreRule`, `IgnoreRuleSet`, `IgnoreMatcher`, `RepositoryContext`, `FileTreeWalkOptions`, and `FileTreeEntry` are safe to share across threads.
- `RepositoryDiscovery` is stateless and can be called concurrently.
- `FileTreeWalker` can be reused to start separate traversals concurrently, but each returned enumeration is single-consumer.
- `IgnoreMatcherEvaluator` is not thread-safe. Use one per concurrent worker.
