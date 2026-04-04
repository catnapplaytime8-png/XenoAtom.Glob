using System.IO.Enumeration;

using BenchmarkDotNet.Attributes;

using LibGit2Sharp;
using GitIgnore = LibGit2Sharp.Ignore;

using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;

namespace XenoAtom.Glob.Benchmarks;

[MemoryDiagnoser]
public class RepositoryTraversalBenchmarks
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    private string _repositoryRoot = null!;
    private FileTreeWalker _walker = null!;
    private FileTreeWalkOptions _walkOptions = null!;
    private Repository _libGitRepository = null!;

    [GlobalSetup]
    public void Setup()
    {
        _repositoryRoot = FindRepositoryRoot();
        _walker = new FileTreeWalker();
        _walkOptions = new FileTreeWalkOptions
        {
            RepositoryContext = RepositoryDiscovery.Discover(_repositoryRoot),
        };
        _libGitRepository = new Repository(_repositoryRoot);

        var xenoCount = EnumerateCurrentRepositoryWithGitIgnore();
        var libGit2SharpCount = EnumerateCurrentRepositoryWithLibGit2SharpIgnoreChecks();
        if (xenoCount != libGit2SharpCount)
        {
            throw new InvalidOperationException(
                $"Repository traversal counts diverged. XenoAtom.Glob={xenoCount}, LibGit2Sharp={libGit2SharpCount}.");
        }
    }

    [Benchmark(Baseline = true)]
    public int EnumerateCurrentRepositoryWithGitIgnore()
        => _walker.Enumerate(_repositoryRoot, _walkOptions).Count();

    [Benchmark]
    public int EnumerateCurrentRepositoryWithLibGit2SharpIgnoreChecks()
        => CountVisibleEntriesWithLibGit2Sharp(_repositoryRoot, _libGitRepository.Ignore);

    [GlobalCleanup]
    public void Cleanup() => _libGitRepository.Dispose();

    private static int CountVisibleEntriesWithLibGit2Sharp(string repositoryRoot, GitIgnore ignore)
    {
        var count = 0;
        var stack = new Stack<TraversalFrame>();
        stack.Push(new TraversalFrame(repositoryRoot, string.Empty, true));

        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            foreach (var child in EnumerateVisibleEntries(frame.FullPath, frame.RelativePath, ignore))
            {
                if (child.IsDirectory)
                {
                    stack.Push(child);
                    continue;
                }

                count++;
            }
        }

        return count;
    }

    private static IEnumerable<TraversalFrame> EnumerateVisibleEntries(string directoryPath, string relativeDirectory, GitIgnore ignore)
    {
        using var enumerator = new RepositoryComparisonEnumerator(directoryPath, relativeDirectory, ignore);
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
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

    private readonly record struct TraversalFrame(string FullPath, string RelativePath, bool IsDirectory);

    private sealed class RepositoryComparisonEnumerator : FileSystemEnumerator<TraversalFrame>
    {
        private readonly string _relativeDirectory;
        private readonly GitIgnore _ignore;

        public RepositoryComparisonEnumerator(string directoryPath, string relativeDirectory, GitIgnore ignore)
            : base(directoryPath, EnumerationOptions)
        {
            _relativeDirectory = relativeDirectory;
            _ignore = ignore;
        }

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        {
            if (entry.FileName.SequenceEqual(".git"))
            {
                return false;
            }

            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var relativePath = CreateRelativePath(entry.FileName, entry.IsDirectory);
            return !_ignore.IsPathIgnored(relativePath);
        }

        protected override TraversalFrame TransformEntry(ref FileSystemEntry entry)
        {
            var relativePath = CreateRelativePath(entry.FileName, isDirectory: false);
            return new TraversalFrame(entry.ToFullPath(), relativePath, entry.IsDirectory);
        }

        private string CreateRelativePath(ReadOnlySpan<char> entryName, bool isDirectory)
        {
            var entryNameText = entryName.ToString();
            var relativePath = _relativeDirectory.Length == 0
                ? entryNameText
                : string.Concat(_relativeDirectory, "/", entryNameText);

            return isDirectory ? string.Concat(relativePath, "/") : relativePath;
        }
    }
}
