// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;
using System.Collections;
using System.IO.Enumeration;

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.IO;

/// <summary>
/// Enumerates files and directories with optional ignore handling.
/// </summary>
/// <remarks>
/// <para><see cref="FileTreeWalker"/> does not keep mutable traversal state on the instance itself, so the same walker may be used to start multiple concurrent enumerations.</para>
/// <para>Each returned enumerable and its active enumerator are single-consumer objects and should not be advanced concurrently from multiple threads.</para>
/// <para>When a shared <see cref="RepositoryContext"/> is supplied, ignore-cache reuse remains thread-safe.</para>
/// </remarks>
public sealed class FileTreeWalker
{
    private static readonly EnumerationOptions DirectoryEnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    /// <summary>
    /// Enumerates a directory tree according to the specified options.
    /// </summary>
    /// <param name="rootPath">The directory to traverse.</param>
    /// <param name="options">Optional traversal options.</param>
    /// <returns>A lazy sequence of file tree entries.</returns>
    /// <remarks>
    /// The returned sequence is lazy and must be enumerated by one consumer at a time. Start a separate enumeration for each concurrent traversal.
    /// </remarks>
    public IEnumerable<FileTreeEntry> Enumerate(string rootPath, FileTreeWalkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        var effectiveOptions = options ?? new FileTreeWalkOptions();
        var fullRootPath = Path.GetFullPath(rootPath);
        var repositoryContext = effectiveOptions.RepositoryContext;
        var includeDirectories = effectiveOptions.IncludeDirectories;
        var followSymbolicLinks = effectiveOptions.FollowSymbolicLinks;
        var cancellationToken = effectiveOptions.CancellationToken;
        IReadOnlyList<IgnoreRuleSet> rootRuleSets = SnapshotRuleSets(effectiveOptions.AdditionalRuleSets);
        var startRelativeDirectory = string.Empty;

        if (repositoryContext is not null)
        {
            startRelativeDirectory = GetRelativeDirectory(repositoryContext.WorkingTreeRoot, fullRootPath, repositoryContext.PathComparison);
        }

        var rootIgnoreStack =
            repositoryContext is not null && rootRuleSets.Count == 0
                ? repositoryContext.GetRepositoryIgnoreStack(startRelativeDirectory)
                : new IgnoreStack(
                    repositoryContext is not null
                        ? MergeRuleSets(repositoryContext.CreateInitialRuleSets(startRelativeDirectory), rootRuleSets)
                        : rootRuleSets,
                    repositoryContext?.PathComparison ?? PathStringComparison.CurrentPlatformDefault);
        return EnumerateCore(
            fullRootPath,
            startRelativeDirectory,
            rootIgnoreStack,
            includeDirectories,
            followSymbolicLinks,
            cancellationToken,
            repositoryContext);
    }

    private IEnumerable<FileTreeEntry> EnumerateCore(
        string directoryPath,
        string relativeDirectory,
        IgnoreStack ignoreStack,
        bool includeDirectories,
        bool followSymbolicLinks,
        CancellationToken cancellationToken,
        RepositoryContext? repositoryContext)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var entry in EnumerateDirectory(
            directoryPath,
            relativeDirectory,
            ignoreStack.Matcher,
            repositoryContext,
            followSymbolicLinks))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = relativeDirectory.Length == 0 ? entry.Name : $"{relativeDirectory}/{entry.Name}";
            var fullPath = Path.Join(directoryPath, entry.Name);
            var yieldedEntry = new FileTreeEntry(
                relativePath,
                fullPath,
                entry.Name,
                entry.IsDirectory,
                entry.Attributes,
                entry.Length,
                entry.CreationTimeUtc,
                entry.LastAccessTimeUtc,
                entry.LastWriteTimeUtc,
                entry.IsHidden);

            if (entry.IsDirectory)
            {
                if (includeDirectories)
                {
                    yield return yieldedEntry;
                }

                if (entry.IsNestedRepositoryBoundary)
                {
                    continue;
                }

                var childIgnoreStack = ignoreStack.PushDirectory(repositoryContext, relativePath);
                foreach (var childEntry in EnumerateCore(
                    fullPath,
                    relativePath,
                    childIgnoreStack,
                    includeDirectories,
                    followSymbolicLinks,
                    cancellationToken,
                    repositoryContext))
                {
                    yield return childEntry;
                }

                continue;
            }

            yield return yieldedEntry;
        }
    }

    private static IReadOnlyList<IgnoreRuleSet> SnapshotRuleSets(IReadOnlyList<IgnoreRuleSet>? ruleSets)
    {
        if (ruleSets is null || ruleSets.Count == 0)
        {
            return [];
        }

        var snapshot = new IgnoreRuleSet[ruleSets.Count];
        for (var index = 0; index < ruleSets.Count; index++)
        {
            snapshot[index] = ruleSets[index];
        }

        return snapshot;
    }

    private static IReadOnlyList<IgnoreRuleSet> MergeRuleSets(
        IReadOnlyList<IgnoreRuleSet> initialRuleSets,
        IReadOnlyList<IgnoreRuleSet> additionalRuleSets)
    {
        if (additionalRuleSets.Count == 0)
        {
            return initialRuleSets;
        }

        var merged = new List<IgnoreRuleSet>(initialRuleSets.Count + additionalRuleSets.Count);
        merged.AddRange(initialRuleSets);
        merged.AddRange(additionalRuleSets);
        return merged;
    }

    private static string GetRelativeDirectory(string repositoryRoot, string rootPath, PathStringComparison comparison)
    {
        if (comparison.Equals(repositoryRoot, rootPath))
        {
            return string.Empty;
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, rootPath);
        return PathNormalizer.NormalizeRelativePath(relativePath, isDirectory: true).Value;
    }

    private static IEnumerable<RawFileSystemEntry> EnumerateDirectory(
        string directoryPath,
        string relativeDirectory,
        IgnoreMatcher ignoreMatcher,
        RepositoryContext? repositoryContext,
        bool followSymbolicLinks)
        => new DirectoryEnumerable(directoryPath, relativeDirectory, ignoreMatcher, repositoryContext, followSymbolicLinks);

    private static bool ShouldIncludeEntry(
        ref FileSystemEntry entry,
        string relativeDirectory,
        IgnoreMatcher ignoreMatcher,
        RepositoryContext? repositoryContext,
        bool followSymbolicLinks)
    {
        if (repositoryContext is not null && entry.FileName.SequenceEqual(".git"))
        {
            return false;
        }

        if (!followSymbolicLinks && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        return !EvaluateIgnore(ignoreMatcher, relativeDirectory, entry.FileName, entry.IsDirectory).IsIgnored;
    }

    private static IgnoreEvaluationResult EvaluateIgnore(IgnoreMatcher matcher, string relativeDirectory, string entryName, bool isDirectory)
        => EvaluateIgnore(matcher, relativeDirectory, entryName.AsSpan(), isDirectory);

    private static IgnoreEvaluationResult EvaluateIgnore(IgnoreMatcher matcher, string relativeDirectory, ReadOnlySpan<char> entryName, bool isDirectory)
    {
        if (relativeDirectory.Length == 0)
        {
            return matcher.EvaluateNormalized(entryName, isDirectory);
        }

        var totalLength = relativeDirectory.Length + 1 + entryName.Length;
        char[]? rentedBuffer = null;
        var buffer = totalLength <= 256
            ? stackalloc char[totalLength]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(totalLength));

        try
        {
            relativeDirectory.AsSpan().CopyTo(buffer);
            buffer[relativeDirectory.Length] = '/';
            entryName.CopyTo(buffer[(relativeDirectory.Length + 1)..]);
            return matcher.EvaluateNormalized(buffer[..totalLength], isDirectory);
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    private static bool IsNestedRepositoryBoundary(string directoryPath, RepositoryContext? repositoryContext)
    {
        if (repositoryContext is null)
        {
            return false;
        }

        var dotGitPath = Path.Combine(directoryPath, ".git");
        return Directory.Exists(dotGitPath) || File.Exists(dotGitPath);
    }

    private readonly record struct RawFileSystemEntry(
        string Name,
        bool IsDirectory,
        FileAttributes Attributes,
        long Length,
        DateTimeOffset CreationTimeUtc,
        DateTimeOffset LastAccessTimeUtc,
        DateTimeOffset LastWriteTimeUtc,
        bool IsHidden,
        bool IsNestedRepositoryBoundary);

    private sealed class DirectoryEnumerable : IEnumerable<RawFileSystemEntry>
    {
        private readonly string _directoryPath;
        private readonly string _relativeDirectory;
        private readonly IgnoreMatcher _ignoreMatcher;
        private readonly RepositoryContext? _repositoryContext;
        private readonly bool _followSymbolicLinks;

        public DirectoryEnumerable(
            string directoryPath,
            string relativeDirectory,
            IgnoreMatcher ignoreMatcher,
            RepositoryContext? repositoryContext,
            bool followSymbolicLinks)
        {
            _directoryPath = directoryPath;
            _relativeDirectory = relativeDirectory;
            _ignoreMatcher = ignoreMatcher;
            _repositoryContext = repositoryContext;
            _followSymbolicLinks = followSymbolicLinks;
        }

        public IEnumerator<RawFileSystemEntry> GetEnumerator()
            => new DirectoryEnumerator(
                _directoryPath,
                _relativeDirectory,
                _ignoreMatcher,
                _repositoryContext,
                _followSymbolicLinks);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DirectoryEnumerator : FileSystemEnumerator<RawFileSystemEntry>
    {
        private readonly string _directoryPath;
        private readonly string _relativeDirectory;
        private readonly IgnoreMatcher _ignoreMatcher;
        private readonly RepositoryContext? _repositoryContext;
        private readonly bool _followSymbolicLinks;

        public DirectoryEnumerator(
            string directoryPath,
            string relativeDirectory,
            IgnoreMatcher ignoreMatcher,
            RepositoryContext? repositoryContext,
            bool followSymbolicLinks)
            : base(directoryPath, DirectoryEnumerationOptions)
        {
            _directoryPath = directoryPath;
            _relativeDirectory = relativeDirectory;
            _ignoreMatcher = ignoreMatcher;
            _repositoryContext = repositoryContext;
            _followSymbolicLinks = followSymbolicLinks;
        }

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
            => FileTreeWalker.ShouldIncludeEntry(ref entry, _relativeDirectory, _ignoreMatcher, _repositoryContext, _followSymbolicLinks);

        protected override RawFileSystemEntry TransformEntry(ref FileSystemEntry entry)
            => new(
                entry.FileName.ToString(),
                entry.IsDirectory,
                entry.Attributes,
                entry.Length,
                entry.CreationTimeUtc,
                entry.LastAccessTimeUtc,
                entry.LastWriteTimeUtc,
                entry.IsHidden,
                entry.IsDirectory && IsNestedRepositoryBoundary(Path.Join(_directoryPath, entry.FileName.ToString()), _repositoryContext));
    }
}
