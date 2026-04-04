# Benchmark Notes

Environment:

- Date: April 4, 2026
- Runtime: `.NET 10.0.5`
- OS: `Windows 11`
- CPU: `AMD Ryzen 9 7950X`
- BenchmarkDotNet: `0.15.6`

## Command Sets

Release-quality runs used the built-in `--release` mode, which fixes the BenchmarkDotNet job at `WarmupCount=6` and `IterationCount=15`.

```sh
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --release --filter "*PathBenchmarks*" "*GlobBenchmarks*" "*IgnoreBenchmarks*" "*TraversalBenchmarks.EnumerateWithoutIgnoreRules*" "*TraversalBenchmarks.EnumerateWithoutIgnoreRulesWithRawRecursiveRuntimeEnumerator*" "*TraversalBenchmarks.EnumerateWithPrunedDirectories*" "*TraversalBenchmarks.EnumerateWhereAllRootEntriesAreSkipped*" "*RepositoryTraversalBenchmarks*"
```

Smoke runs are intentionally shorter and are only for harness validation or quick local checks. They must not be used for release claims.

```sh
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --smoke --filter "*PathBenchmarks*" "*GlobBenchmarks*"
```

Raw BenchmarkDotNet exports are generated under `src/BenchmarkDotNet.Artifacts/results/`.

## Allocation Investigation

The earlier one-iteration snapshot that attributed `56 B` to `MatchLiteralPath` and `104 B` to ignore evaluation was stale.

Release-grade reruns show:

- `PathBenchmarks.NormalizeAlreadyNormalizedPath` allocates `0 B`.
- `PathBenchmarks.NormalizeWindowsStylePath` and `NormalizeUnixStylePath` allocate `80 B`, which matches the slow normalization path materializing a new string.
- `GlobBenchmarks.MatchLiteralPath` allocates `0 B` for already-normalized input.
- `GlobBenchmarks.MatchLiteralPathWithNormalizationRequired` allocates `56 B`; the allocation only appears once normalization has to produce a new normalized path string.
- `GlobBenchmarks.EvaluateIgnoreDecision` and `EvaluateIgnoreDecisionPreNormalizedCore` both allocate `32 B` in the tested directory-rule scenario, which rules out public path normalization as the source for that case.
- The expanded `IgnoreBenchmarks` matrix is allocation-free for the public `EvaluateIgnoreDecision` scenarios that use basename and deep full-path rules, but some internal pre-normalized-core variants still show `32 B`.

The actionable conclusion is narrower than the original report: normalization is a confirmed allocation source only when the input actually needs normalization, and any remaining ignore-evaluation allocation is inside matcher internals rather than at the public string entry point.

## Path And Glob Results

| Area | Benchmark | Mean | Allocated |
| --- | --- | ---: | ---: |
| path | `NormalizeAlreadyNormalizedPath` | `20.49 ns` | `0 B` |
| path | `NormalizeWindowsStylePath` | `40.08 ns` | `80 B` |
| path | `NormalizeUnixStylePath` | `50.01 ns` | `80 B` |
| glob | `ParseRecursivePattern` | `251.34 ns` | `2032 B` |
| glob | `MatchLiteralPath` | `24.47 ns` | `0 B` |
| glob | `MatchLiteralPathFailure` | `23.86 ns` | `0 B` |
| glob | `MatchLiteralPathPreNormalizedCore` | `7.20 ns` | `0 B` |
| glob | `MatchLiteralPathWithNormalizationRequired` | `34.70 ns` | `56 B` |
| glob | `MatchRecursivePath` | `70.00 ns` | `0 B` |
| glob | `MatchRecursivePathFailure` | `72.46 ns` | `0 B` |
| glob | `MatchCharClassPath` | `43.31 ns` | `0 B` |
| glob | `MatchCharClassPathFailure` | `39.15 ns` | `0 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecision` | `42.51 ns` | `32 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecisionPreNormalizedCore` | `27.28 ns` | `32 B` |

## Ignore Scaling

The ignore benchmark matrix now covers:

- basename hit and miss cases
- deep full-path hit and miss cases
- rule-set sizes `1`, `10`, `100`, and `1000`

Selected public-entry-point results:

| Scenario | 1 rule | 10 rules | 100 rules | 1000 rules |
| --- | ---: | ---: | ---: | ---: |
| `BasenameHit` | `100.99 ns` | `1.68 us` | `18.09 us` | `173.09 us` |
| `BasenameMiss` | `106.15 ns` | `1.70 us` | `17.82 us` | `177.25 us` |
| `DeepHit` | `980.11 ns` | `5.05 us` | `46.30 us` | `454.59 us` |
| `DeepMiss` | `1.01 us` | `5.94 us` | `47.05 us` | `519.39 us` |

The public benchmark path stayed allocation-free across that matrix. The pre-normalized-core variants are useful for internal investigation, but they are not currently evidence for a public span-overload win by themselves.

## Traversal Results

The misleading one-level raw baseline was removed. The no-ignore comparison is now recursive versus recursive on the same corpus shape.

| Corpus | Raw Recursive No Ignore | `EnumerateWithoutIgnoreRules` | `EnumerateWithPrunedDirectories` | `EnumerateWhereAllRootEntriesAreSkipped` |
| --- | ---: | ---: | ---: | ---: |
| `Small` | `670.70 us` | `717.19 us` | `741.07 us` | `69.90 us` |
| `Medium` | `1.78 ms` | `1.94 ms` | `2.76 ms` | `98.18 us` |
| `Large` | `3.30 ms` | `3.72 ms` | `6.93 ms` | `150.79 us` |

For the same recursive no-ignore traversal shape, the library overhead versus a raw recursive `FileSystemEnumerator<T>` baseline is roughly:

- `+6.9%` on the small corpus
- `+8.7%` on the medium corpus
- `+12.8%` on the large corpus

## Current Repository

Release run over this repository:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `EnumerateCurrentRepositoryWithGitIgnore` | `4.686 ms` | `52.42 KB` |
| `EnumerateCurrentRepositoryWithLibGit2SharpIgnoreChecks` | `8.350 ms` | `74.50 KB` |

On this machine and repository snapshot, the repository-aware traversal benchmark is about `1.79x` faster than the LibGit2Sharp comparison and allocates about `1.42x` less memory.
