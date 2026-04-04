using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class PathBenchmarks
{
    [Benchmark]
    public string NormalizeAlreadyNormalizedPath() => PathNormalizer.NormalizeRelativePath("src/generated/nested/file.txt").Value;

    [Benchmark]
    public string NormalizeWindowsStylePath() => PathNormalizer.NormalizeRelativePath(@"src\generated\\nested\file.txt").Value;

    [Benchmark]
    public string NormalizeUnixStylePath() => PathNormalizer.NormalizeRelativePath("src/generated//nested/file.txt").Value;
}
