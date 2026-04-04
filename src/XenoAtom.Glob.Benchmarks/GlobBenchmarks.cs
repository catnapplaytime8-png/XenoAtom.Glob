using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class GlobBenchmarks
{
    private GlobPattern _literalPattern = null!;
    private GlobPattern _recursivePattern = null!;
    private GlobPattern _charClassPattern = null!;
    private GlobPattern _starLiteralPattern = null!;
    private GlobCompiledPattern _literalCompiledPattern = null!;
    private NormalizedPath _literalNormalizedPath;
    private NormalizedPath _ignoreNormalizedPath;
    private IgnoreMatcher _ignoreMatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        _literalPattern = GlobPattern.Parse("src/app/file.cs");
        _recursivePattern = GlobPattern.Parse("src/**/file.cs");
        _charClassPattern = GlobPattern.Parse("src/file[0-9].cs");
        _starLiteralPattern = GlobPattern.Parse("*generated*");
        _literalCompiledPattern = GlobParser.TryParse("src/app/file.cs", GlobParserOptions.Default).Pattern;
        _literalNormalizedPath = PathNormalizer.NormalizeRelativePath("src/app/file.cs");
        _ignoreMatcher = new IgnoreMatcher(
            IgnoreRuleSet.ParseGitIgnore("""
                *.tmp
                obj/
                !keep.tmp
                """));
        _ignoreNormalizedPath = PathNormalizer.NormalizeRelativePath("obj/build/output.tmp");
    }

    [Benchmark]
    public GlobPattern ParseRecursivePattern() => GlobPattern.Parse("src/**/generated/*.g.cs");

    [Benchmark]
    public bool MatchLiteralPath() => _literalPattern.IsMatch("src/app/file.cs");

    [Benchmark]
    public bool MatchLiteralPathFailure() => _literalPattern.IsMatch("src/app/file.txt");

    [Benchmark]
    public bool MatchLiteralPathPreNormalizedCore() => _literalCompiledPattern.Match(_literalNormalizedPath, PathStringComparison.Ordinal);

    [Benchmark]
    public bool MatchLiteralPathWithNormalizationRequired() => _literalPattern.IsMatch(@"src\app\file.cs");

    [Benchmark]
    public bool MatchRecursivePath() => _recursivePattern.IsMatch("src/nested/deep/file.cs");

    [Benchmark]
    public bool MatchRecursivePathFailure() => _recursivePattern.IsMatch("src/nested/deep/file.txt");

    [Benchmark]
    public bool MatchCharClassPath() => _charClassPattern.IsMatch("src/file7.cs");

    [Benchmark]
    public bool MatchCharClassPathFailure() => _charClassPattern.IsMatch("src/filex.cs");

    [Benchmark]
    public bool MatchStarLiteralPath() => _starLiteralPattern.IsMatch("report-generated-output.cs");

    [Benchmark]
    public bool MatchStarLiteralPathFailure() => _starLiteralPattern.IsMatch("report-output.cs");

    [Benchmark]
    public bool EvaluateIgnoreDecision() => _ignoreMatcher.Evaluate("obj/build/output.tmp").IsIgnored;

    [Benchmark]
    public bool EvaluateIgnoreDecisionPreNormalizedCore() => _ignoreMatcher.Evaluate(_ignoreNormalizedPath).IsIgnored;
}
