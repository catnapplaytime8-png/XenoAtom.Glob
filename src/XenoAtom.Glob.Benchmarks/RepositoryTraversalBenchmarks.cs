using BenchmarkDotNet.Attributes;

using LibGit2Sharp;

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class RepositoryTraversalBenchmarks
{
    private string _repositoryRoot = null!;
    private string[] _repositoryFiles = null!;
    private IgnoreMatcher _matcher = null!;
    private Repository _libGitRepository = null!;

    [GlobalSetup]
    public void Setup()
    {
        _repositoryRoot = FindRepositoryRoot();
        _repositoryFiles = CollectRepositoryFiles(_repositoryRoot).ToArray();
        _matcher = BuildMatcherFromRepository(_repositoryRoot);
        _libGitRepository = new Repository(_repositoryRoot);

        var xenoCount = MatchCollectedRepositoryFilesWithGitIgnore();
        var libGit2SharpCount = MatchCollectedRepositoryFilesWithLibGit2SharpIgnoreChecks();
        if (xenoCount != libGit2SharpCount)
        {
            throw new InvalidOperationException(
                $"Repository match counts diverged. XenoAtom.Glob={xenoCount}, LibGit2Sharp={libGit2SharpCount}.");
        }
    }

    [Benchmark(Baseline = true)]
    public int MatchCollectedRepositoryFilesWithGitIgnore()
    {
        var count = 0;
        foreach (var path in _repositoryFiles)
        {
            if (!_matcher.Evaluate(path).IsIgnored)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark]
    public int MatchCollectedRepositoryFilesWithLibGit2SharpIgnoreChecks()
    {
        var count = 0;
        foreach (var path in _repositoryFiles)
        {
            if (!_libGitRepository.Ignore.IsPathIgnored(path))
            {
                count++;
            }
        }

        return count;
    }

    [GlobalCleanup]
    public void Cleanup() => _libGitRepository.Dispose();

    private static IgnoreMatcher BuildMatcherFromRepository(string repositoryRoot)
    {
        var repository = RepositoryDiscovery.Discover(repositoryRoot);
        var ruleSets = new List<IgnoreRuleSet>();

        if (repository.GlobalExcludePath is { } globalExcludePath && File.Exists(globalExcludePath))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(globalExcludePath),
                sourcePath: globalExcludePath,
                sourceKind: IgnoreRuleSourceKind.GlobalExclude));
        }

        var infoExcludePath = repository.InfoExcludePath;
        if (File.Exists(infoExcludePath))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(infoExcludePath),
                sourcePath: infoExcludePath,
                sourceKind: IgnoreRuleSourceKind.RepositoryExclude));
        }

        foreach (var gitIgnorePath in EnumerateGitIgnoreFiles(repositoryRoot))
        {
            var baseDirectory = Path.GetDirectoryName(gitIgnorePath)!;
            var relativeBase = Path.GetRelativePath(repositoryRoot, baseDirectory).Replace('\\', '/');
            if (relativeBase == ".")
            {
                relativeBase = string.Empty;
            }

            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(gitIgnorePath),
                baseDirectory: relativeBase,
                sourcePath: gitIgnorePath,
                sourceKind: IgnoreRuleSourceKind.PerDirectory));
        }

        return new IgnoreMatcher(ruleSets);
    }

    private static IEnumerable<string> EnumerateGitIgnoreFiles(string repositoryRoot)
    {
        var stack = new Stack<string>();
        stack.Push(repositoryRoot);

        while (stack.Count > 0)
        {
            var currentDirectory = stack.Pop();

            foreach (var directory in Directory.EnumerateDirectories(currentDirectory))
            {
                var directoryName = Path.GetFileName(directory);
                if (string.Equals(directoryName, ".git", StringComparison.Ordinal) || IsSymlink(directory))
                {
                    continue;
                }

                stack.Push(directory);
            }

            var gitIgnorePath = Path.Combine(currentDirectory, ".gitignore");
            if (File.Exists(gitIgnorePath) && !IsSymlink(gitIgnorePath))
            {
                yield return gitIgnorePath;
            }
        }
    }

    private static IEnumerable<string> CollectRepositoryFiles(string repositoryRoot)
    {
        var stack = new Stack<string>();
        stack.Push(repositoryRoot);

        while (stack.Count > 0)
        {
            var currentDirectory = stack.Pop();

            foreach (var directory in Directory.EnumerateDirectories(currentDirectory))
            {
                var directoryName = Path.GetFileName(directory);
                if (string.Equals(directoryName, ".git", StringComparison.Ordinal) || IsSymlink(directory))
                {
                    continue;
                }

                stack.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(currentDirectory))
            {
                if (IsSymlink(file))
                {
                    continue;
                }

                yield return Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            }
        }
    }

    private static bool IsSymlink(string path)
    {
        var attributes = File.GetAttributes(path);
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Unable to locate the repository root from '{AppContext.BaseDirectory}'.");
    }
}
