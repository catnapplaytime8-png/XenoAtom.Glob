# XenoAtom.Glob [![ci](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml/badge.svg)](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/XenoAtom.Glob.svg)](https://www.nuget.org/packages/XenoAtom.Glob/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/XenoAtom.Glob/main/img/XenoAtom.Glob.png">

A high-performance .NET glob library with gitignore compatibility.

## ✨ Features

- Compiled glob patterns with `*`, `?`, character classes, escapes, and `**`
- Git-compatible ignore parsing and layered evaluation
- Explicit ignore dialect selection for `.gitignore` and `.ignore`
- Repository discovery for `.git` directories and gitfiles
- Repository-aware ignore matching that honors `core.ignorecase`
- Reusable repository contexts that cache parsed and compiled repository ignore state across traversals
- Public `ReadOnlySpan<char>` overloads for glob matching, ignore evaluation, and ignore-file parsing
- Reusable `IgnoreMatcherEvaluator` instances for allocation-free repeated ignore checks
- Tree walking with ignore-aware directory pruning and captured file metadata on `FileTreeEntry`
- Differential tests against the Git CLI for compatibility-sensitive behavior
- Benchmark coverage for synthetic corpora and real repository traversal against the working-tree ignore stack
- Clear separation between ignore evaluation and tracked-file state from the Git index
- `net10`+ compatible and NativeAOT ready

## 🚀 Quick Example

```csharp
using XenoAtom.Glob;
using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;
using XenoAtom.Glob.Ignore;

var pattern = GlobPattern.Parse("src/**/file.cs");
var matches = pattern.IsMatch("src/nested/file.cs");

var ruleSet = IgnoreRuleSet.ParseGitIgnore("""
    *.tmp
    build/
    !build/keep.tmp
    """);
var ignoreRuleSet = IgnoreRuleSet.Parse("""
    *.cache
    """, IgnoreDialect.IgnoreFile);
var ignoreMatcher = new IgnoreMatcher(ruleSet);
var ignored = ignoreMatcher.Evaluate("build/output.tmp").IsIgnored;
var spanIgnored = ignoreMatcher.Evaluate(@"build\output.tmp".AsSpan()).IsIgnored;
using var ignoreEvaluator = ignoreMatcher.CreateEvaluator();
var hotPathIgnored = ignoreEvaluator.Evaluate("build/output.tmp").IsIgnored;

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

## 📖 User Guide

For more details on how to use XenoAtom.Glob, please visit the [user guide](https://github.com/XenoAtom/XenoAtom.Glob/blob/main/doc/readme.md).

## Thread Safety

- `GlobPattern`, `IgnoreRule`, `IgnoreRuleSet`, `IgnoreMatcher`, `RepositoryContext`, `FileTreeWalkOptions`, `FileTreeEntry`, and parse/evaluation result values are immutable or internally synchronized and can be shared across threads.
- `RepositoryDiscovery` is stateless and can be called concurrently.
- `FileTreeWalker` instances can start multiple concurrent traversals, but each returned enumeration should be consumed by only one thread at a time.
- `IgnoreMatcherEvaluator` is intentionally reusable but not thread-safe. Create one evaluator per concurrent worker and dispose it when that worker is done.

## 🪪 License

This software is released under the [BSD-2-Clause license](https://opensource.org/licenses/BSD-2-Clause). 

## 🤗 Author

Alexandre Mutel aka [xoofx](https://xoofx.github.io).
