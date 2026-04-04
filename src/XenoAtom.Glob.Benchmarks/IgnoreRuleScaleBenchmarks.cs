using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class IgnoreRuleScaleBenchmarks
{
    private IgnoreMatcher _matcher = null!;
    private IgnoreMatcherEvaluator _evaluator = null!;
    private string[] _candidatePaths = null!;

    [Params(100, 1000)]
    public int RuleCount { get; set; }

    [Params(1000, 10000)]
    public int FileCount { get; set; }

    [Params("BasenameSuffixHit", "RecursiveDeepHit", "RecursiveDeepMiss", "PathPrefixHit", "PathPrefixMiss")]
    public string Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rules = new List<string>(RuleCount);

        switch (Scenario)
        {
            case "BasenameSuffixHit":
                for (var i = 0; i < RuleCount - 1; i++)
                {
                    rules.Add($"*.noise{i}");
                }

                rules.Add("*.tmp");
                _candidatePaths = CreateCandidatePaths(FileCount, i => $"src/dir{i % 128}/file{i}.tmp");
                break;

            case "RecursiveDeepHit":
                for (var i = 0; i < RuleCount - 1; i++)
                {
                    rules.Add($"module{i}/**/generated/**/*.noise{i}");
                }

                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePaths = CreateCandidatePaths(FileCount, i => $"src/features/feature{i % 64}/generated/deep/file{i}.g.cs");
                break;

            case "RecursiveDeepMiss":
                for (var i = 0; i < RuleCount - 1; i++)
                {
                    rules.Add($"module{i}/**/generated/**/*.noise{i}");
                }

                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePaths = CreateCandidatePaths(FileCount, i => $"src/features/feature{i % 64}/generated/deep/file{i}.cs");
                break;

            case "PathPrefixHit":
                for (var i = 0; i < RuleCount; i++)
                {
                    rules.Add($"bucket{i}/generated/**");
                }

                _candidatePaths = CreateCandidatePaths(FileCount, i => $"bucket{i % RuleCount}/generated/deep/file{i}.tmp");
                break;

            case "PathPrefixMiss":
                for (var i = 0; i < RuleCount; i++)
                {
                    rules.Add($"bucket{i}/generated/**");
                }

                _candidatePaths = CreateCandidatePaths(FileCount, i => $"other{i % RuleCount}/generated/deep/file{i}.tmp");
                break;

            default:
                throw new InvalidOperationException($"Unsupported scenario '{Scenario}'.");
        }

        _matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));
        _evaluator = _matcher.CreateEvaluator();
    }

    [Benchmark]
    public int EvaluateBatch()
    {
        var ignoredCount = 0;
        foreach (var candidatePath in _candidatePaths)
        {
            if (_evaluator.Evaluate(candidatePath).IsIgnored)
            {
                ignoredCount++;
            }
        }

        return ignoredCount;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _evaluator.Dispose();
    }

    private static string[] CreateCandidatePaths(int fileCount, Func<int, string> generator)
    {
        var candidatePaths = new string[fileCount];
        for (var i = 0; i < candidatePaths.Length; i++)
        {
            candidatePaths[i] = generator(i);
        }

        return candidatePaths;
    }
}
