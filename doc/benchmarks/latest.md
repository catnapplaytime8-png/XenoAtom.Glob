# Benchmark Snapshot

Environment:

- Date: April 4, 2026
- Runtime: `.NET 10.0.5`
- OS: `Windows 11`
- CPU: `AMD Ryzen 9 7950X`
- Commands:

```sh
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --job short --warmupCount 1 --iterationCount 1 --filter "*.MatchLiteralPath" "*.EvaluateIgnoreDecision" "*.NormalizeWindowsStylePath"
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --job short --warmupCount 1 --iterationCount 1 --filter "*TraversalBenchmarks.EnumerateWithPrunedDirectories*"
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --job short --warmupCount 1 --iterationCount 1 --filter "*TraversalBenchmarks.EnumerateWhereAllRootEntriesAreSkipped*"
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --job short --warmupCount 1 --iterationCount 1 --filter "*TraversalBenchmarks.EnumerateRootEntriesWithRawRuntimeEnumerator*"
dotnet-trace collect --profile cpu-sampling --output <temp>.nettrace -- dotnet <temp-harness>\TraceHarness.dll
```

Core results:

| Area | Benchmark | Mean | Allocated |
| --- | --- | ---: | ---: |
| glob | `GlobBenchmarks.MatchLiteralPath` | `31.00 ns` | `56 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecision` | `61.14 ns` | `104 B` |
| path | `PathBenchmarks.NormalizeWindowsStylePath` | `47.55 ns` | `80 B` |

Ignore-scaling results:

| Effective rules | Mean | Allocated |
| ---: | ---: | ---: |
| `1` | `53.45 ns` | `104 B` |
| `10` | `128.68 ns` | `104 B` |
| `100` | `840.96 ns` | `104 B` |
| `1000` | `7,946.14 ns` | `104 B` |

Traversal pruning results:

| Corpus size | Mean | Allocated |
| --- | ---: | ---: |
| `Small` | `827.85 us` | `40.80 KB` |
| `Medium` | `3,075.4 us` | `164.30 KB` |
| `Large` | `7,782.1 us` | `412.74 KB` |

Traversal skipped-root results:

| Corpus size | Mean | Allocated |
| --- | ---: | ---: |
| `Small` | `70.59 us` | `440 B` |
| `Medium` | `109.11 us` | `440 B` |
| `Large` | `168.40 us` | `440 B` |

Raw runtime skipped-root baseline:

| Corpus size | Mean | Allocated |
| --- | ---: | ---: |
| `Small` | `27.52 us` | `128 B` |
| `Medium` | `54.25 us` | `128 B` |
| `Large` | `106.42 us` | `128 B` |

Artifacts:

- Raw BenchmarkDotNet reports are generated under `BenchmarkDotNet.Artifacts/results/`.

Notes:

- This is a short-run smoke snapshot intended to validate the benchmark harness and record a first baseline for path, glob, ignore, and traversal scenarios.
- The traversal benchmark now exercises size-scaled corpora, while the ignore benchmark records rule-count scaling across `1`, `10`, `100`, and `1000` effective rules.
- The latest tuning removed hot-path segment splitting, prefix string construction, and unnecessary normalization copies for already-canonical relative paths.
- The latest traversal tuning removed needless child rule-stack cloning when a directory has no local `.gitignore`, keeps relative ignore checks on a temporary span buffer before allocating emitted paths, and avoids materializing `FileName` strings for ignored entries.
- Repository contexts now cache parsed ignore files by path plus file metadata, invalidate those cached parses when `.gitignore`, `.git/info/exclude`, or global exclude files change, and reuse the compiled repository-root ignore stack across repeated traversals when its ignore-file dependency metadata is unchanged.
- The ignore matcher hot path now uses indexed loops instead of interface-typed `foreach` enumeration, removing boxed enumerator allocations from repeated rule evaluation.
- Relative to the first short-run snapshot, `GlobBenchmarks.EvaluateIgnoreDecision` improved from `131.87 ns` / `472 B` to `61.14 ns` / `104 B`.
- A profiler-backed traversal trace over a dedicated harness showed the dominant remaining work in `FileTreeWalker` enumeration, directory `.gitignore` existence checks (`File.Exists`, `RepositoryContext.TryLoadDirectoryRuleSet`, `RepositoryContext.CreateChildRuleSets`), and underlying handle open/close calls rather than in per-entry ignore evaluation alone.
- The skipped-entry benchmark now uses repository-scoped excludes from `.git/info/exclude` so the root directory can be measured without a visible working-tree `.gitignore` entry, and the dedicated raw-runtime baseline shows the underlying one-level `FileSystemEnumerator<T>` floor at `128 B` while the library path remains flat at `440 B`, which closes the skipped-entry allocation gate.
- Release-quality benchmarking should use longer BenchmarkDotNet jobs and be repeated on each supported platform before publishing a stable package.
