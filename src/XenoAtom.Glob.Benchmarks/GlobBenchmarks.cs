using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class GlobBenchmarks
{
    private GlobPattern _literalPattern = null!;
    private GlobPattern _recursivePattern = null!;
    private IgnoreMatcher _ignoreMatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        _literalPattern = GlobPattern.Parse("src/app/file.cs");
        _recursivePattern = GlobPattern.Parse("src/**/file.cs");
        _ignoreMatcher = new IgnoreMatcher(
            IgnoreRuleSet.ParseGitIgnore("""
                *.tmp
                obj/
                !keep.tmp
                """));
    }

    [Benchmark]
    public GlobPattern ParseRecursivePattern() => GlobPattern.Parse("src/**/generated/*.g.cs");

    [Benchmark]
    public bool MatchLiteralPath() => _literalPattern.IsMatch("src/app/file.cs");

    [Benchmark]
    public bool MatchRecursivePath() => _recursivePattern.IsMatch("src/nested/deep/file.cs");

    [Benchmark]
    public bool EvaluateIgnoreDecision() => _ignoreMatcher.Evaluate("obj/build/output.tmp").IsIgnored;
}
