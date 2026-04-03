// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Git;

/// <summary>
/// Represents a discovered Git working tree and its ignore-related metadata.
/// </summary>
public sealed class RepositoryContext
{
    private readonly object _cacheLock = new();
    private CachedIgnoreFile? _globalExcludeCache;
    private CachedIgnoreFile? _infoExcludeCache;
    private readonly Dictionary<string, CachedIgnoreFile> _perDirectoryCache = new(StringComparer.Ordinal);

    internal RepositoryContext(string workingTreeRoot, string gitDirectory, string? globalExcludePath, PathStringComparison pathComparison)
    {
        WorkingTreeRoot = workingTreeRoot;
        GitDirectory = gitDirectory;
        GlobalExcludePath = globalExcludePath;
        PathComparison = pathComparison;
    }

    /// <summary>
    /// Gets the working tree root directory.
    /// </summary>
    public string WorkingTreeRoot { get; }

    /// <summary>
    /// Gets the resolved Git directory.
    /// </summary>
    public string GitDirectory { get; }

    /// <summary>
    /// Gets the resolved global exclude path when available.
    /// </summary>
    public string? GlobalExcludePath { get; }

    internal PathStringComparison PathComparison { get; }

    /// <summary>
    /// Gets the repository-scoped exclude file path.
    /// </summary>
    public string InfoExcludePath => Path.Combine(GitDirectory, "info", "exclude");

    internal IReadOnlyList<IgnoreRuleSet> CreateInitialRuleSets(string startRelativeDirectory)
    {
        var ruleSets = new List<IgnoreRuleSet>();
        if (GlobalExcludePath is not null &&
            TryLoadIgnoreFile(
                GlobalExcludePath,
                baseDirectory: string.Empty,
                IgnoreRuleSourceKind.GlobalExclude,
                ref _globalExcludeCache,
                out var globalExcludeRuleSet))
        {
            ruleSets.Add(globalExcludeRuleSet);
        }

        if (TryLoadIgnoreFile(
            InfoExcludePath,
            baseDirectory: string.Empty,
            IgnoreRuleSourceKind.RepositoryExclude,
            ref _infoExcludeCache,
            out var infoExcludeRuleSet))
        {
            ruleSets.Add(infoExcludeRuleSet);
        }

        if (startRelativeDirectory.Length == 0)
        {
            TryAddDirectoryRuleSet(ruleSets, string.Empty);
            return ruleSets;
        }

        TryAddDirectoryRuleSet(ruleSets, string.Empty);
        var current = string.Empty;
        foreach (var segment in PathNormalizer.NormalizeRelativePath(startRelativeDirectory, isDirectory: true).EnumerateSegments())
        {
            current = current.Length == 0 ? segment.ToString() : $"{current}/{segment}";
            TryAddDirectoryRuleSet(ruleSets, current);
        }

        return ruleSets;
    }

    internal IReadOnlyList<IgnoreRuleSet> CreateChildRuleSets(IReadOnlyList<IgnoreRuleSet> currentRuleSets, string childRelativeDirectory)
    {
        if (!TryLoadDirectoryRuleSet(childRelativeDirectory, out var ruleSet))
        {
            return currentRuleSets;
        }

        var ruleSets = new List<IgnoreRuleSet>(currentRuleSets.Count + 1);
        ruleSets.AddRange(currentRuleSets);
        ruleSets.Add(ruleSet);
        return ruleSets;
    }

    private void TryAddDirectoryRuleSet(List<IgnoreRuleSet> ruleSets, string relativeDirectory)
    {
        if (!TryLoadDirectoryRuleSet(relativeDirectory, out var ruleSet))
        {
            return;
        }

        ruleSets.Add(ruleSet);
    }

    private bool TryLoadDirectoryRuleSet(string relativeDirectory, out IgnoreRuleSet ruleSet)
    {
        var gitIgnorePath = relativeDirectory.Length == 0
            ? Path.Combine(WorkingTreeRoot, ".gitignore")
            : Path.Combine(WorkingTreeRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar), ".gitignore");

        if (!TryGetIgnoreFileMetadata(gitIgnorePath, out var lastWriteTimeUtc, out var length))
        {
            ruleSet = null!;
            return false;
        }

        lock (_cacheLock)
        {
            if (_perDirectoryCache.TryGetValue(relativeDirectory, out var cachedRuleSet) &&
                cachedRuleSet.LastWriteTimeUtc == lastWriteTimeUtc &&
                cachedRuleSet.Length == length)
            {
                ruleSet = cachedRuleSet.RuleSet;
                return true;
            }
        }

        var parsedRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(gitIgnorePath),
            baseDirectory: relativeDirectory,
            sourcePath: gitIgnorePath,
            sourceKind: IgnoreRuleSourceKind.PerDirectory);

        lock (_cacheLock)
        {
            _perDirectoryCache[relativeDirectory] = new CachedIgnoreFile(lastWriteTimeUtc, length, parsedRuleSet);
        }

        ruleSet = parsedRuleSet;
        return true;
    }

    private bool TryLoadIgnoreFile(
        string path,
        string baseDirectory,
        IgnoreRuleSourceKind sourceKind,
        ref CachedIgnoreFile? cache,
        out IgnoreRuleSet ruleSet)
    {
        if (!TryGetIgnoreFileMetadata(path, out var lastWriteTimeUtc, out var length))
        {
            ruleSet = null!;
            return false;
        }

        lock (_cacheLock)
        {
            if (cache is CachedIgnoreFile cachedRuleSet &&
                cachedRuleSet.LastWriteTimeUtc == lastWriteTimeUtc &&
                cachedRuleSet.Length == length)
            {
                ruleSet = cachedRuleSet.RuleSet;
                return true;
            }
        }

        var parsedRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(path),
            baseDirectory: baseDirectory,
            sourcePath: path,
            sourceKind: sourceKind);

        lock (_cacheLock)
        {
            cache = new CachedIgnoreFile(lastWriteTimeUtc, length, parsedRuleSet);
        }

        ruleSet = parsedRuleSet;
        return true;
    }

    private static bool TryGetIgnoreFileMetadata(string path, out DateTime lastWriteTimeUtc, out long length)
    {
        if (!File.Exists(path) || IsSymlink(path))
        {
            lastWriteTimeUtc = default;
            length = default;
            return false;
        }

        var fileInfo = new FileInfo(path);
        lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        length = fileInfo.Length;
        return true;
    }

    private static bool IsSymlink(string path)
    {
        var attributes = File.GetAttributes(path);
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }

    private readonly record struct CachedIgnoreFile(DateTime LastWriteTimeUtc, long Length, IgnoreRuleSet RuleSet);
}
