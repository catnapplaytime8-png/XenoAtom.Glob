using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class ParseBenchmarks
{
    private string _ignoreContent = null!;

    [GlobalSetup]
    public void Setup()
    {
        var lines = new List<string>(128);
        for (var index = 0; index < 64; index++)
        {
            lines.Add($"*.noise{index}");
            lines.Add($"src/generated/{index}/**/*.g.cs");
        }

        _ignoreContent = string.Join('\n', lines);
    }

    [Benchmark]
    public GlobPattern ParseRecursivePattern()
        => GlobPattern.Parse("src/**/generated/*.g.cs");

    [Benchmark]
    public IgnoreRuleSet ParseGitIgnoreString()
        => IgnoreRuleSet.ParseGitIgnore(_ignoreContent);

    [Benchmark]
    public IgnoreRuleSet ParseGitIgnoreSpan()
        => IgnoreRuleSet.ParseGitIgnore(_ignoreContent.AsSpan());
}
