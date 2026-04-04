using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class IgnoreBenchmarks
{
    private IgnoreMatcher _matcher = null!;
    private string _candidatePath = null!;
    private NormalizedPath _normalizedCandidatePath;

    [Params(1, 10, 100, 1000)]
    public int EffectiveRuleCount { get; set; }

    [Params("BasenameHit", "BasenameMiss", "DeepHit", "DeepMiss")]
    public string Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rules = new List<string>(EffectiveRuleCount);
        for (var i = 0; i < Math.Max(0, EffectiveRuleCount - 1); i++)
        {
            rules.Add($"noise{i}/**/skip{i}.tmp");
        }

        switch (Scenario)
        {
            case "BasenameHit":
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.tmp";
                break;
            case "BasenameMiss":
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.bin";
                break;
            case "DeepHit":
                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePath = "src/features/reports/generated/deep/output.g.cs";
                break;
            case "DeepMiss":
                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePath = "src/features/reports/generated/deep/output.cs";
                break;
            default:
                throw new InvalidOperationException($"Unsupported scenario '{Scenario}'.");
        }

        _matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));
        _normalizedCandidatePath = PathNormalizer.NormalizeRelativePath(_candidatePath);
    }

    [Benchmark]
    public bool EvaluateIgnoreDecision() => _matcher.Evaluate(_candidatePath).IsIgnored;

    [Benchmark]
    public bool EvaluateIgnoreDecisionPreNormalizedCore() => _matcher.Evaluate(_normalizedCandidatePath).IsIgnored;
}
