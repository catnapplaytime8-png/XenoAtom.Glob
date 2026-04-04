using System.IO.Enumeration;

using BenchmarkDotNet.Attributes;

using XenoAtom.Glob.IO;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class TraversalBenchmarks
{
    private BenchmarkRepositoryFixture _fixture = null!;
    private BenchmarkRepositoryFixture _deepIgnoreFixture = null!;
    private BenchmarkRepositoryFixture _prunedFixture = null!;
    private BenchmarkRepositoryFixture _skippedFixture = null!;
    private FileTreeWalker _walker = null!;
    private FileTreeWalkOptions _noIgnoreOptions = null!;
    private FileTreeWalkOptions _shallowIgnoreOptions = null!;
    private FileTreeWalkOptions _deepIgnoreOptions = null!;
    private FileTreeWalkOptions _prunedOptions = null!;
    private FileTreeWalkOptions _skippedOptions = null!;

    [Params("Small", "Medium", "Large")]
    public string CorpusSize { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var (wideDirectories, wideFilesPerDirectory, generatedFiles, deepDepth, prunedDirectories) = CorpusSize switch
        {
            "Small" => (12, 4, 8, 8, 24),
            "Medium" => (60, 8, 40, 12, 100),
            "Large" => (120, 12, 120, 20, 250),
            _ => throw new InvalidOperationException($"Unsupported corpus size '{CorpusSize}'."),
        };

        _fixture = new BenchmarkRepositoryFixture();
        _fixture.WriteAllText(".gitignore", """
            bin/
            obj/
            generated/
            temp/
            *.tmp
            """);
        _fixture.WriteAllText("src/.gitignore", """
            generated/
            !generated/include.txt
            """);
        _fixture.CreateDeepTree(depth: deepDepth, filesPerDirectory: 4);
        _fixture.CreateWideTree(directoryCount: wideDirectories, filesPerDirectory: wideFilesPerDirectory);
        for (var i = 0; i < generatedFiles; i++)
        {
            _fixture.WriteAllText($"src/generated/file{i}.g.cs", string.Empty);
            _fixture.WriteAllText($"src/generated/include{i}.txt", string.Empty);
            _fixture.WriteAllText($"temp/output{i}.tmp", string.Empty);
            _fixture.WriteAllText($"obj/output{i}.obj", string.Empty);
        }

        var repository = _fixture.InitializeGitRepository();
        _deepIgnoreFixture = new BenchmarkRepositoryFixture();
        _deepIgnoreFixture.WriteAllText(".gitignore", "ignored-root/\n");
        _deepIgnoreFixture.CreateDeepTree(depth: deepDepth + 8, filesPerDirectory: 4);
        var current = "deep";
        for (var i = 0; i < Math.Max(6, deepDepth / 2); i++)
        {
            _deepIgnoreFixture.WriteAllText($"{current}/.gitignore", "ignored-local/\n");
            _deepIgnoreFixture.WriteAllText($"{current}/keep{i}.txt", string.Empty);
            _deepIgnoreFixture.WriteAllText($"{current}/ignored-local/skip{i}.txt", string.Empty);
            current = $"{current}/level{i}";
        }

        var deepRepository = _deepIgnoreFixture.InitializeGitRepository();
        _prunedFixture = new BenchmarkRepositoryFixture();
        _prunedFixture.WriteAllText(".gitignore", """
            temp/
            cache/
            build/
            """);
        for (var i = 0; i < prunedDirectories; i++)
        {
            _prunedFixture.WriteAllText($"temp/dir{i}/skip{i}.txt", string.Empty);
            _prunedFixture.WriteAllText($"cache/dir{i}/skip{i}.txt", string.Empty);
            _prunedFixture.WriteAllText($"build/dir{i}/skip{i}.txt", string.Empty);
            _prunedFixture.WriteAllText($"src/dir{i}/keep{i}.txt", string.Empty);
        }

        var prunedRepository = _prunedFixture.InitializeGitRepository();
        _skippedFixture = new BenchmarkRepositoryFixture();
        var skippedRepository = _skippedFixture.InitializeGitRepository();
        _skippedFixture.WriteAllText(".git/info/exclude", """
            skip*/
            *.tmp
            """);
        for (var i = 0; i < prunedDirectories; i++)
        {
            _skippedFixture.WriteAllText($"skip{i}/drop{i}.txt", string.Empty);
            _skippedFixture.WriteAllText($"entry{i}.tmp", string.Empty);
        }

        _walker = new FileTreeWalker();
        _noIgnoreOptions = new FileTreeWalkOptions();
        _shallowIgnoreOptions = new FileTreeWalkOptions { RepositoryContext = repository };
        _deepIgnoreOptions = new FileTreeWalkOptions { RepositoryContext = deepRepository };
        _prunedOptions = new FileTreeWalkOptions { RepositoryContext = prunedRepository };
        _skippedOptions = new FileTreeWalkOptions { RepositoryContext = skippedRepository };
    }

    [Benchmark]
    public int EnumerateWithoutIgnoreRules() => _walker.Enumerate(_fixture.RootPath, _noIgnoreOptions).Count();

    [Benchmark(Baseline = true)]
    public int EnumerateWithoutIgnoreRulesWithRawRecursiveRuntimeEnumerator() => CountFilesRawRecursively(_fixture.RootPath);

    [Benchmark]
    public int EnumerateWithShallowIgnoreRules() => _walker.Enumerate(_fixture.RootPath, _shallowIgnoreOptions).Count();

    [Benchmark]
    public int EnumerateWithDeepNestedIgnoreRules() => _walker.Enumerate(_deepIgnoreFixture.RootPath, _deepIgnoreOptions).Count();

    [Benchmark]
    public int EnumerateWithPrunedDirectories() => _walker.Enumerate(_prunedFixture.RootPath, _prunedOptions).Count();

    [Benchmark]
    public int EnumerateWhereAllRootEntriesAreSkipped() => _walker.Enumerate(_skippedFixture.RootPath, _skippedOptions).Count();

    [GlobalCleanup]
    public void Cleanup()
    {
        _fixture.Dispose();
        _deepIgnoreFixture.Dispose();
        _prunedFixture.Dispose();
        _skippedFixture.Dispose();
    }

    private static int CountFilesRawRecursively(string directoryPath)
    {
        var count = 0;
        var directories = new Stack<string>();
        directories.Push(directoryPath);

        while (directories.Count > 0)
        {
            using var enumerator = new RawCountingEnumerator(directories.Pop());
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (current.IsDirectory)
                {
                    directories.Push(current.FullPath);
                    continue;
                }

                count++;
            }
        }

        return count;
    }

    private readonly record struct RawEntry(string FullPath, bool IsDirectory);

    private sealed class RawCountingEnumerator : FileSystemEnumerator<RawEntry>
    {
        private static readonly EnumerationOptions Options = new()
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
        };

        public RawCountingEnumerator(string directoryPath)
            : base(directoryPath, Options)
        {
        }

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
            => (entry.Attributes & FileAttributes.ReparsePoint) == 0;

        protected override RawEntry TransformEntry(ref FileSystemEntry entry)
            => new(entry.ToFullPath(), entry.IsDirectory);
    }
}
