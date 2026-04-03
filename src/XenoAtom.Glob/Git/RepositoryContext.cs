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
    private bool _hasGlobalExcludeState;
    private CachedIgnoreFileState _globalExcludeState;
    private bool _hasInfoExcludeState;
    private CachedIgnoreFileState _infoExcludeState;
    private readonly Dictionary<string, CachedIgnoreFileState> _perDirectoryCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedIgnoreStack> _repositoryIgnoreStackCache = new(StringComparer.Ordinal);

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

    internal IgnoreStack GetRepositoryIgnoreStack(string startRelativeDirectory)
    {
        lock (_cacheLock)
        {
            if (_repositoryIgnoreStackCache.TryGetValue(startRelativeDirectory, out var cachedStack) &&
                IsRepositoryIgnoreStackFresh(cachedStack))
            {
                return cachedStack.Stack;
            }
        }

        var dependencies = new List<CachedIgnoreStackDependency>();
        var ruleSets = CreateInitialRuleSets(startRelativeDirectory, dependencies);
        var stack = new IgnoreStack(ruleSets, PathComparison);

        lock (_cacheLock)
        {
            _repositoryIgnoreStackCache[startRelativeDirectory] = new CachedIgnoreStack(stack, dependencies.ToArray());
        }

        return stack;
    }

    internal IReadOnlyList<IgnoreRuleSet> CreateInitialRuleSets(string startRelativeDirectory)
        => CreateInitialRuleSets(startRelativeDirectory, dependencies: null);

    private IReadOnlyList<IgnoreRuleSet> CreateInitialRuleSets(string startRelativeDirectory, List<CachedIgnoreStackDependency>? dependencies)
    {
        var ruleSets = new List<IgnoreRuleSet>();
        if (GlobalExcludePath is not null &&
            TryLoadIgnoreFile(
                GlobalExcludePath,
                baseDirectory: string.Empty,
                IgnoreRuleSourceKind.GlobalExclude,
                dependencies,
                ref _hasGlobalExcludeState,
                ref _globalExcludeState,
                out var globalExcludeRuleSet))
        {
            ruleSets.Add(globalExcludeRuleSet);
        }

        if (TryLoadIgnoreFile(
            InfoExcludePath,
            baseDirectory: string.Empty,
            IgnoreRuleSourceKind.RepositoryExclude,
            dependencies,
            ref _hasInfoExcludeState,
            ref _infoExcludeState,
            out var infoExcludeRuleSet))
        {
            ruleSets.Add(infoExcludeRuleSet);
        }

        if (startRelativeDirectory.Length == 0)
        {
            TryAddDirectoryRuleSet(ruleSets, string.Empty, dependencies);
            return ruleSets;
        }

        TryAddDirectoryRuleSet(ruleSets, string.Empty, dependencies);
        var current = string.Empty;
        foreach (var segment in PathNormalizer.NormalizeRelativePath(startRelativeDirectory, isDirectory: true).EnumerateSegments())
        {
            current = current.Length == 0 ? segment.ToString() : $"{current}/{segment}";
            TryAddDirectoryRuleSet(ruleSets, current, dependencies);
        }

        return ruleSets;
    }

    internal IReadOnlyList<IgnoreRuleSet> CreateChildRuleSets(IReadOnlyList<IgnoreRuleSet> currentRuleSets, string childRelativeDirectory)
    {
        if (!TryLoadDirectoryRuleSet(childRelativeDirectory, dependencies: null, out var ruleSet))
        {
            return currentRuleSets;
        }

        var ruleSets = new List<IgnoreRuleSet>(currentRuleSets.Count + 1);
        ruleSets.AddRange(currentRuleSets);
        ruleSets.Add(ruleSet);
        return ruleSets;
    }

    private void TryAddDirectoryRuleSet(List<IgnoreRuleSet> ruleSets, string relativeDirectory, List<CachedIgnoreStackDependency>? dependencies)
    {
        if (!TryLoadDirectoryRuleSet(relativeDirectory, dependencies, out var ruleSet))
        {
            return;
        }

        ruleSets.Add(ruleSet);
    }

    private bool TryLoadDirectoryRuleSet(string relativeDirectory, List<CachedIgnoreStackDependency>? dependencies, out IgnoreRuleSet ruleSet)
    {
        var gitIgnorePath = relativeDirectory.Length == 0
            ? Path.Combine(WorkingTreeRoot, ".gitignore")
            : Path.Combine(WorkingTreeRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar), ".gitignore");

        if (!TryLoadIgnoreFile(
            gitIgnorePath,
            baseDirectory: relativeDirectory,
            IgnoreRuleSourceKind.PerDirectory,
            dependencies,
            relativeDirectory,
            out ruleSet))
        {
            return false;
        }

        return true;
    }

    private bool TryLoadIgnoreFile(
        string path,
        string baseDirectory,
        IgnoreRuleSourceKind sourceKind,
        List<CachedIgnoreStackDependency>? dependencies,
        ref bool hasState,
        ref CachedIgnoreFileState state,
        out IgnoreRuleSet ruleSet)
    {
        return TryLoadIgnoreFile(path, baseDirectory, sourceKind, dependencies, key: null, ref hasState, ref state, out ruleSet);
    }

    private bool TryLoadIgnoreFile(
        string path,
        string baseDirectory,
        IgnoreRuleSourceKind sourceKind,
        List<CachedIgnoreStackDependency>? dependencies,
        string relativeDirectory,
        out IgnoreRuleSet ruleSet)
    {
        CachedIgnoreFileState state;
        var hasState = false;
        lock (_cacheLock)
        {
            if (_perDirectoryCache.TryGetValue(relativeDirectory, out state))
            {
                hasState = true;
            }
        }

        var found = TryLoadIgnoreFile(path, baseDirectory, sourceKind, dependencies, relativeDirectory, ref hasState, ref state, out ruleSet);
        lock (_cacheLock)
        {
            _perDirectoryCache[relativeDirectory] = state;
        }

        return found;
    }

    private bool TryLoadIgnoreFile(
        string path,
        string baseDirectory,
        IgnoreRuleSourceKind sourceKind,
        List<CachedIgnoreStackDependency>? dependencies,
        string? key,
        ref bool hasState,
        ref CachedIgnoreFileState state,
        out IgnoreRuleSet ruleSet)
    {
        var exists = TryGetIgnoreFileMetadata(path, out var lastWriteTimeUtc, out var length);
        dependencies?.Add(new CachedIgnoreStackDependency(path, exists, lastWriteTimeUtc, length));
        lock (_cacheLock)
        {
            if (hasState &&
                state.Exists == exists &&
                (!exists || (state.LastWriteTimeUtc == lastWriteTimeUtc && state.Length == length)))
            {
                ruleSet = state.RuleSet!;
                return exists;
            }
        }

        if (!exists)
        {
            lock (_cacheLock)
            {
                UpdateCachedState(key, ref hasState, ref state, new CachedIgnoreFileState(false, default, 0, null));
            }

            ruleSet = null!;
            return false;
        }

        var parsedRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(path),
            baseDirectory: baseDirectory,
            sourcePath: path,
            sourceKind: sourceKind);

        lock (_cacheLock)
        {
            UpdateCachedState(key, ref hasState, ref state, new CachedIgnoreFileState(true, lastWriteTimeUtc, length, parsedRuleSet));
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

    private void UpdateCachedState(string? key, ref bool hasState, ref CachedIgnoreFileState state, CachedIgnoreFileState newState)
    {
        if (hasState && state.Equals(newState))
        {
            return;
        }

        hasState = true;
        state = newState;
        if (key is not null)
        {
            _perDirectoryCache[key] = newState;
        }
    }

    private static bool IsRepositoryIgnoreStackFresh(CachedIgnoreStack cachedStack)
    {
        foreach (var dependency in cachedStack.Dependencies)
        {
            var exists = TryGetIgnoreFileMetadata(dependency.Path, out var lastWriteTimeUtc, out var length);
            if (exists != dependency.Exists)
            {
                return false;
            }

            if (exists &&
                (lastWriteTimeUtc != dependency.LastWriteTimeUtc || length != dependency.Length))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct CachedIgnoreFileState(bool Exists, DateTime LastWriteTimeUtc, long Length, IgnoreRuleSet? RuleSet);
    private readonly record struct CachedIgnoreStackDependency(string Path, bool Exists, DateTime LastWriteTimeUtc, long Length);
    private readonly record struct CachedIgnoreStack(IgnoreStack Stack, CachedIgnoreStackDependency[] Dependencies);
}
