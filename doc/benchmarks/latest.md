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
dotnet run --project src/XenoAtom.Glob.Benchmarks/XenoAtom.Glob.Benchmarks.csproj -c Release -- --release --filter "*PathBenchmarks*" "*GlobBenchmarks*" "*IgnoreBenchmarks*" "*SpanApiBenchmarks*" "*ParseBenchmarks*" "*TraversalBenchmarks.EnumerateWithoutIgnoreRules*" "*TraversalBenchmarks.EnumerateWithoutIgnoreRulesWithRawRecursiveRuntimeEnumerator*" "*TraversalBenchmarks.EnumerateWithPrunedDirectories*" "*TraversalBenchmarks.EnumerateWhereAllRootEntriesAreSkipped*" "*RepositoryTraversalBenchmarks*"
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
- `SpanApiBenchmarks.MatchLiteralPathWithNormalizationRequiredSpan` removes that public normalization allocation entirely and drops the same scenario to `0 B`.
- `SpanApiBenchmarks.EvaluateIgnoreDecisionWithNormalizationRequiredString` allocates `96 B`, while the span overload removes that normalization cost entirely and drops the same scenario to `0 B`.
- `ParseBenchmarks.ParseRecursivePattern` now allocates `1.91 KB`, down from the earlier `2032 B` snapshot after the span-based parser cleanup.
- The expanded `IgnoreBenchmarks` matrix is still allocation-free for the public `EvaluateIgnoreDecision` scenarios that use basename and deep full-path rules, and the new indexed-rule scenarios stay allocation-free as well.

The actionable conclusion is now broader and better evidenced:

- normalization is a confirmed allocation source only when the input actually needs normalization
- the new public span overloads remove that public normalization allocation for matching and ignore evaluation
- the remaining `32 B` ignore-allocation case is specific to the existing directory-rule benchmark shape, not to public string normalization
- parser allocation work is measurable on glob compilation, while ignore-file parse totals are still dominated by the retained rule object graph

## Path And Glob Results

| Area | Benchmark | Mean | Allocated |
| --- | --- | ---: | ---: |
| path | `NormalizeAlreadyNormalizedPath` | `30.72 ns` | `0 B` |
| path | `NormalizeWindowsStylePath` | `53.22 ns` | `80 B` |
| path | `NormalizeUnixStylePath` | `62.71 ns` | `80 B` |
| glob | `ParseRecursivePattern` | `249.01 ns` | `1.91 KB` |
| glob | `MatchLiteralPath` | `24.09 ns` | `0 B` |
| glob | `MatchLiteralPathFailure` | `23.10 ns` | `0 B` |
| glob | `MatchLiteralPathPreNormalizedCore` | `4.22 ns` | `0 B` |
| glob | `MatchLiteralPathWithNormalizationRequired` | `39.56 ns` | `56 B` |
| glob | `MatchRecursivePath` | `76.25 ns` | `0 B` |
| glob | `MatchRecursivePathFailure` | `81.80 ns` | `0 B` |
| glob | `MatchCharClassPath` | `46.68 ns` | `0 B` |
| glob | `MatchCharClassPathFailure` | `42.40 ns` | `0 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecision` | `60.22 ns` | `32 B` |
| ignore | `GlobBenchmarks.EvaluateIgnoreDecisionPreNormalizedCore` | `36.34 ns` | `32 B` |

## Public Span API Results

These targeted release benchmarks exercise the public `ReadOnlySpan<char>` entry points on normalization-required inputs:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `MatchLiteralPathWithNormalizationRequiredString` | `39.13 ns` | `56 B` |
| `MatchLiteralPathWithNormalizationRequiredSpan` | `28.94 ns` | `0 B` |
| `EvaluateIgnoreDecisionWithNormalizationRequiredString` | `77.28 ns` | `96 B` |
| `EvaluateIgnoreDecisionWithNormalizationRequiredSpan` | `51.56 ns` | `0 B` |

## Ignore Scaling

The ignore benchmark matrix now covers:

- basename hit and miss cases
- deep full-path hit and miss cases
- rule-set sizes `1`, `10`, `100`, and `1000`
- targeted large-rule-set exact-basename and extension-suffix cases that exercise the matcher index

Selected public-entry-point results:

| Scenario | 1 rule | 10 rules | 100 rules | 1000 rules |
| --- | ---: | ---: | ---: | ---: |
| `BasenameHit` | `80.98 ns` | `1.77 us` | `18.72 us` | `174.20 us` |
| `BasenameMiss` | `83.44 ns` | `1.78 us` | `18.80 us` | `173.14 us` |
| `DeepHit` | `1.11 us` | `5.92 us` | `47.33 us` | `464.05 us` |
| `DeepMiss` | `1.12 us` | `5.26 us` | `47.04 us` | `459.95 us` |

The baseline deep-rule matrix still scales with rule count because those scenarios are intentionally dominated by non-indexable full-path patterns.

The new index-focused release reruns show the intended large-rule-set win for simple basename-heavy rule sets:

| Scenario | 1000 rules |
| --- | ---: |
| `IndexedExactHit` | `168.58 ns` |
| `IndexedExactMiss` | `150.86 ns` |
| `IndexedExtensionHit` | `107.62 ns` |
| `IndexedExtensionMiss` | `90.75 ns` |

Those indexed scenarios stay allocation-free and avoid the `173 us` to `519 us` scaling behavior seen in the deep fallback-heavy matrix.

## Parse Results

The parser benchmarks now cover both glob compilation and full ignore-file parsing:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `ParseRecursivePattern` | `243.10 ns` | `1.91 KB` |
| `ParseGitIgnoreString` | `32.89 us` | `228.85 KB` |
| `ParseGitIgnoreSpan` | `32.64 us` | `228.85 KB` |

The glob-parser cleanup reduced the transient allocation footprint of recursive pattern compilation. The ignore-file span entry point removes the initial full-content copy and per-line string splitting, but the total benchmark allocation is still dominated by the retained `IgnoreRule`, token, and pattern object graph that both public parse paths must build.

## Traversal Results

The misleading one-level raw baseline was removed. The no-ignore comparison is now recursive versus recursive on the same corpus shape.

| Corpus | Raw Recursive No Ignore | `EnumerateWithoutIgnoreRules` | `EnumerateWithPrunedDirectories` | `EnumerateWhereAllRootEntriesAreSkipped` |
| --- | ---: | ---: | ---: | ---: |
| `Small` | `746.99 us` | `791.52 us` | `840.53 us` | `76.42 us` |
| `Medium` | `1.97 ms` | `2.14 ms` | `3.14 ms` | `106.84 us` |
| `Large` | `3.61 ms` | `4.03 ms` | `7.60 ms` | `161.90 us` |

For the same recursive no-ignore traversal shape, the library overhead versus a raw recursive `FileSystemEnumerator<T>` baseline is roughly:

- `+6.0%` on the small corpus
- `+8.5%` on the medium corpus
- `+11.6%` on the large corpus

## Current Repository

Release run over this repository:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `EnumerateCurrentRepositoryWithGitIgnore` | `2.460 ms` | `53.72 KB` |
| `EnumerateCurrentRepositoryWithLibGit2SharpIgnoreChecks` | `8.518 ms` | `76.44 KB` |

On this machine and repository snapshot, the repository-aware traversal benchmark is about `3.46x` faster than the LibGit2Sharp comparison and allocates about `1.42x` less memory.
