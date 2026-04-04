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

    [Params(
        "BasenameHit",
        "BasenameMiss",
        "DeepHit",
        "DeepMiss",
        "IndexedExactHit",
        "IndexedExactMiss",
        "IndexedExtensionHit",
        "IndexedExtensionMiss")]
    public string Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rules = new List<string>(EffectiveRuleCount);

        switch (Scenario)
        {
            case "BasenameHit":
                AddDeepRuleNoise(rules);
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.tmp";
                break;
            case "BasenameMiss":
                AddDeepRuleNoise(rules);
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.bin";
                break;
            case "DeepHit":
                AddDeepRuleNoise(rules);
                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePath = "src/features/reports/generated/deep/output.g.cs";
                break;
            case "DeepMiss":
                AddDeepRuleNoise(rules);
                rules.Add("src/**/generated/**/*.g.cs");
                _candidatePath = "src/features/reports/generated/deep/output.cs";
                break;
            case "IndexedExactHit":
                AddIndexedExactRuleNoise(rules);
                rules.Add("target.generated");
                _candidatePath = "src/obj/build/target.generated";
                break;
            case "IndexedExactMiss":
                AddIndexedExactRuleNoise(rules);
                rules.Add("target.generated");
                _candidatePath = "src/obj/build/missing.generated";
                break;
            case "IndexedExtensionHit":
                AddIndexedExtensionRuleNoise(rules);
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.tmp";
                break;
            case "IndexedExtensionMiss":
                AddIndexedExtensionRuleNoise(rules);
                rules.Add("*.tmp");
                _candidatePath = "src/obj/build/output.bin";
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

    private void AddDeepRuleNoise(List<string> rules)
    {
        for (var i = 0; i < Math.Max(0, EffectiveRuleCount - 1); i++)
        {
            rules.Add($"noise{i}/**/skip{i}.tmp");
        }
    }

    private void AddIndexedExactRuleNoise(List<string> rules)
    {
        for (var i = 0; i < Math.Max(0, EffectiveRuleCount - 1); i++)
        {
            rules.Add($"artifact{i}.generated");
        }
    }

    private void AddIndexedExtensionRuleNoise(List<string> rules)
    {
        for (var i = 0; i < Math.Max(0, EffectiveRuleCount - 1); i++)
        {
            rules.Add($"*.noise{i}");
        }
    }
}
