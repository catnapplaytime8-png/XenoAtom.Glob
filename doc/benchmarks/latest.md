# Benchmark Snapshot

Environment:

- Date: April 3, 2026
- Runtime: `.NET 10.0.5`
- OS: `Windows 11`
- CPU: `AMD Ryzen 9 7950X`
- Command:

```sh
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --job short --warmupCount 1 --iterationCount 1 --filter "*.MatchLiteralPath" "*.EvaluateIgnoreDecision" "*.NormalizeWindowsStylePath" "*.EnumerateWithPrunedDirectories"
```

Core results:

| Area | Benchmark | Mean | Allocated |
| --- | --- | ---: | ---: |
| glob | `GlobBenchmarks.MatchLiteralPath` | `31.00 ns` | `56 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecision` | `60.41 ns` | `104 B` |
| path | `PathBenchmarks.NormalizeWindowsStylePath` | `47.55 ns` | `80 B` |

Ignore-scaling results:

| Effective rules | Mean | Allocated |
| ---: | ---: | ---: |
| `1` | `51.33 ns` | `104 B` |
| `10` | `119.88 ns` | `104 B` |
| `100` | `816.41 ns` | `104 B` |
| `1000` | `7,158.15 ns` | `104 B` |

Traversal pruning results:

| Corpus size | Mean | Allocated |
| --- | ---: | ---: |
| `Small` | `979.0 us` | `71.77 KB` |
| `Medium` | `3,239.9 us` | `222.59 KB` |
| `Large` | `6,977.9 us` | `524.94 KB` |

Artifacts:

- Raw BenchmarkDotNet reports are generated under `BenchmarkDotNet.Artifacts/results/`.

Notes:

- This is a short-run smoke snapshot intended to validate the benchmark harness and record a first baseline for path, glob, ignore, and traversal scenarios.
- The traversal benchmark now exercises size-scaled corpora, while the ignore benchmark records rule-count scaling across `1`, `10`, `100`, and `1000` effective rules.
- The latest tuning removed hot-path segment splitting, prefix string construction, and unnecessary normalization copies for already-canonical relative paths.
- Relative to the first short-run snapshot, `GlobBenchmarks.EvaluateIgnoreDecision` improved from `131.87 ns` / `472 B` to `60.41 ns` / `104 B`.
- Release-quality benchmarking should use longer BenchmarkDotNet jobs and be repeated on each supported platform before publishing a stable package.
