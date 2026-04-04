# XenoAtom.Glob [![ci](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml/badge.svg)](https://github.com/XenoAtom/XenoAtom.Glob/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/XenoAtom.Glob.svg)](https://www.nuget.org/packages/XenoAtom.Glob/)

<img align="right" width="256px" height="256px" src="https://raw.githubusercontent.com/XenoAtom/XenoAtom.Glob/main/img/XenoAtom.Glob.png">

A high-performance .NET glob library with gitignore compatibility.

## ✨ Features

- Compiled glob patterns with `*`, `?`, character classes, escapes, and `**`
- Git-compatible ignore parsing for `.gitignore` and `.ignore`
- Repository-aware ignore matching with repository discovery and `core.ignorecase` support
- Low-allocation APIs for hot paths, including `ReadOnlySpan<char>` overloads and reusable evaluators
- Ignore-aware file tree walking with directory pruning and file metadata capture
- Differential tests against the Git CLI, with `net10`+ and NativeAOT support

## 🚀 Quick Example

```csharp
using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;

var repository = RepositoryDiscovery.Discover(@"C:\code\my-repo");
var walker = new FileTreeWalker();

// Walk the working tree while honoring the repository ignore rules.
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
