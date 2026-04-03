using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class IgnoreBenchmarks
{
    private IgnoreMatcher _matcher = null!;

    [Params(1, 10, 100, 1000)]
    public int EffectiveRuleCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rules = new List<string>(EffectiveRuleCount + 1);
        for (var i = 0; i < EffectiveRuleCount; i++)
        {
            rules.Add($"noise{i}.tmp");
        }

        rules.Add("obj/");
        _matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));
    }

    [Benchmark]
    public bool EvaluateIgnoreDecision() => _matcher.Evaluate("obj/build/output.tmp").IsIgnored;
}
