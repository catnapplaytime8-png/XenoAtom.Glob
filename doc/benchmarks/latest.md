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
| `Small` | `964.59 us` | `61.59 KB` |
| `Medium` | `3,261.7 us` | `185.09 KB` |
| `Large` | `7,518.8 us` | `433.53 KB` |

Traversal skipped-root results:

| Corpus size | Mean | Allocated |
| --- | ---: | ---: |
| `Small` | `299.32 us` | `24.25 KB` |
| `Medium` | `340.39 us` | `36.13 KB` |
| `Large` | `412.71 us` | `59.56 KB` |

Artifacts:

- Raw BenchmarkDotNet reports are generated under `BenchmarkDotNet.Artifacts/results/`.

Notes:

- This is a short-run smoke snapshot intended to validate the benchmark harness and record a first baseline for path, glob, ignore, and traversal scenarios.
- The traversal benchmark now exercises size-scaled corpora, while the ignore benchmark records rule-count scaling across `1`, `10`, `100`, and `1000` effective rules.
- The latest tuning removed hot-path segment splitting, prefix string construction, and unnecessary normalization copies for already-canonical relative paths.
- The latest traversal tuning removed needless child rule-stack cloning when a directory has no local `.gitignore`, keeps relative ignore checks on a temporary span buffer before allocating emitted paths, and avoids materializing `FileName` strings for ignored entries.
- Relative to the first short-run snapshot, `GlobBenchmarks.EvaluateIgnoreDecision` improved from `131.87 ns` / `472 B` to `61.14 ns` / `104 B`.
- A profiler-backed traversal trace over a dedicated harness showed the dominant remaining work in `FileTreeWalker` enumeration, directory `.gitignore` existence checks (`File.Exists`, `RepositoryContext.TryLoadDirectoryRuleSet`, `RepositoryContext.CreateChildRuleSets`), and underlying handle open/close calls rather than in per-entry ignore evaluation alone.
- The skipped-entry benchmark improved materially, but it still allocates tens of kilobytes per whole traversal operation; the stricter skipped-entry allocation gate remains intentionally open.
- Release-quality benchmarking should use longer BenchmarkDotNet jobs and be repeated on each supported platform before publishing a stable package.
