using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class SpanApiBenchmarks
{
    private GlobPattern _globPattern = null!;
    private IgnoreMatcher _ignoreMatcher = null!;
    private string _globCandidate = null!;
    private string _ignoreCandidate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _globPattern = GlobPattern.Parse("src/app/file.cs");
        _ignoreMatcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.tmp
            obj/
            !keep.tmp
            """));
        _globCandidate = @"src\app\file.cs";
        _ignoreCandidate = @"obj\build\output.tmp";
    }

    [Benchmark]
    public bool MatchLiteralPathWithNormalizationRequiredString()
        => _globPattern.IsMatch(_globCandidate);

    [Benchmark]
    public bool MatchLiteralPathWithNormalizationRequiredSpan()
        => _globPattern.IsMatch(_globCandidate.AsSpan());

    [Benchmark]
    public bool EvaluateIgnoreDecisionWithNormalizationRequiredString()
        => _ignoreMatcher.Evaluate(_ignoreCandidate).IsIgnored;

    [Benchmark]
    public bool EvaluateIgnoreDecisionWithNormalizationRequiredSpan()
        => _ignoreMatcher.Evaluate(_ignoreCandidate.AsSpan()).IsIgnored;
}
