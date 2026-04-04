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
| path | `NormalizeAlreadyNormalizedPath` | `20.49 ns` | `0 B` |
| path | `NormalizeWindowsStylePath` | `40.08 ns` | `80 B` |
| path | `NormalizeUnixStylePath` | `50.01 ns` | `80 B` |
| glob | `ParseRecursivePattern` | `242.56 ns` | `1.91 KB` |
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

## Public Span API Results

These targeted release benchmarks exercise the public `ReadOnlySpan<char>` entry points on normalization-required inputs:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `MatchLiteralPathWithNormalizationRequiredString` | `40.88 ns` | `56 B` |
| `MatchLiteralPathWithNormalizationRequiredSpan` | `27.89 ns` | `0 B` |
| `EvaluateIgnoreDecisionWithNormalizationRequiredString` | `81.29 ns` | `96 B` |
| `EvaluateIgnoreDecisionWithNormalizationRequiredSpan` | `49.38 ns` | `0 B` |

## Ignore Scaling

The ignore benchmark matrix now covers:

- basename hit and miss cases
- deep full-path hit and miss cases
- rule-set sizes `1`, `10`, `100`, and `1000`
- targeted large-rule-set exact-basename and extension-suffix cases that exercise the matcher index

Selected public-entry-point results:

| Scenario | 1 rule | 10 rules | 100 rules | 1000 rules |
| --- | ---: | ---: | ---: | ---: |
| `BasenameHit` | `100.99 ns` | `1.68 us` | `18.09 us` | `173.09 us` |
| `BasenameMiss` | `106.15 ns` | `1.70 us` | `17.82 us` | `177.25 us` |
| `DeepHit` | `980.11 ns` | `5.05 us` | `46.30 us` | `454.59 us` |
| `DeepMiss` | `1.01 us` | `5.94 us` | `47.05 us` | `519.39 us` |

The baseline deep-rule matrix still scales with rule count because those scenarios are intentionally dominated by non-indexable full-path patterns.

The new index-focused release reruns show the intended large-rule-set win for simple basename-heavy rule sets:

| Scenario | 1000 rules |
| --- | ---: |
| `IndexedExactHit` | `165.69 ns` |
| `IndexedExactMiss` | `150.17 ns` |
| `IndexedExtensionHit` | `108.87 ns` |
| `IndexedExtensionMiss` | `89.16 ns` |

Those indexed scenarios stay allocation-free and avoid the `173 us` to `519 us` scaling behavior seen in the deep fallback-heavy matrix.

## Parse Results

The parser benchmarks now cover both glob compilation and full ignore-file parsing:

| Benchmark | Mean | Allocated |
| --- | ---: | ---: |
| `ParseRecursivePattern` | `242.56 ns` | `1.91 KB` |
| `ParseGitIgnoreString` | `32.78 us` | `228.85 KB` |
| `ParseGitIgnoreSpan` | `34.42 us` | `228.85 KB` |

The glob-parser cleanup reduced the transient allocation footprint of recursive pattern compilation. The ignore-file span entry point removes the initial full-content copy and per-line string splitting, but the total benchmark allocation is still dominated by the retained `IgnoreRule`, token, and pattern object graph that both public parse paths must build.

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
