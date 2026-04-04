# XenoAtom.Glob [![ci](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml/badge.svg)](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/XenoAtom.Glob.svg)](https://www.nuget.org/packages/XenoAtom.Glob/)

<img align="right" width="256px" height="256px" src="https://raw.githubusercontent.com/XenoAtom/XenoAtom.Glob/main/img/XenoAtom.Glob.png">

A high-performance .NET glob library with gitignore compatibility.

## What It Is For

XenoAtom.Glob gives you three focused building blocks:

- `GlobPattern` to match relative paths against compiled glob patterns
- `IgnoreRuleSet` and `IgnoreMatcher` to evaluate `.gitignore`-style rules
- `RepositoryDiscovery` and `FileTreeWalker` to walk a Git working tree while honoring ignore files

## Install

```bash
dotnet add package XenoAtom.Glob
```

## Quick Start

```csharp
using XenoAtom.Glob;
using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;
using XenoAtom.Glob.Ignore;

var pattern = GlobPattern.Parse("src/**/file.cs");
Console.WriteLine(pattern.IsMatch("src/nested/file.cs"));   // true
Console.WriteLine(pattern.IsMatch(@"src\nested\file.cs"));  // true

var rules = IgnoreRuleSet.ParseGitIgnore("""
    *.tmp
    generated/*
    !generated/include.txt
    """);

var matcher = new IgnoreMatcher(rules);
Console.WriteLine(matcher.Evaluate("file.tmp").IsIgnored);               // true
Console.WriteLine(matcher.Evaluate("generated/code.cs").IsIgnored);      // true
Console.WriteLine(matcher.Evaluate("generated/include.txt").IsIgnored);  // false
Console.WriteLine(matcher.Evaluate("generated", isDirectory: true).IsIgnored); // false

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo");
var walker = new FileTreeWalker();

foreach (var entry in walker.Enumerate(repository.WorkingTreeRoot, new FileTreeWalkOptions
{
    RepositoryContext = repository,
}))
{
    Console.WriteLine(entry.RelativePath);
}
```

## Path Rules

- Public matching APIs work with relative paths, not absolute paths.
- Both `/` and `\` are accepted as input path separators.
- Returned relative paths are normalized to `/`.
- `..` segments are rejected.
- When you evaluate a directory directly, pass `isDirectory: true`.
- `FileTreeWalker` handles directory detection for you.

## Which API Should I Use?

- Use `GlobPattern` when you only need pattern matching.
- Use `IgnoreMatcher` when you already have ignore file content and want `.gitignore` semantics.
- Use `RepositoryDiscovery` plus `FileTreeWalker` when you want Git-aware traversal of a real working tree.

## Case Sensitivity

- `GlobPattern` matching is case-sensitive.
- Standalone `IgnoreMatcher` uses the current platform default path comparison.
- Repository-aware traversal uses the repository's `core.ignorecase` value when available.

## Git Compatibility Scope

Repository-aware APIs handle:

- `.gitignore` files from the repository root down to the current directory
- `.git/info/exclude`
- `core.excludesFile`
- `.git` directories and gitfiles
- nested checked-out repositories and submodules as traversal boundaries

Intentionally out of scope:

- tracked-file state from the Git index

## User Guide

The full guide is in [doc/readme.md](doc/readme.md).

## Thread Safety

- `GlobPattern`, `IgnoreRule`, `IgnoreRuleSet`, `IgnoreMatcher`, `RepositoryContext`, `FileTreeWalkOptions`, and `FileTreeEntry` are safe to share.
- `IgnoreMatcherEvaluator` is reusable but not thread-safe.
- `FileTreeWalker` can start multiple enumerations, but each individual enumeration should be consumed by one thread at a time.

## License

This software is released under the [BSD-2-Clause license](https://opensource.org/licenses/BSD-2-Clause).

## Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
