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
    private int _ignoreStateVersion;
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
        var ruleSets = CreateInitialRuleSets(startRelativeDirectory);
        lock (_cacheLock)
        {
            if (_repositoryIgnoreStackCache.TryGetValue(startRelativeDirectory, out var cachedStack) &&
                cachedStack.Version == _ignoreStateVersion)
            {
                return cachedStack.Stack;
            }

            var stack = new IgnoreStack(ruleSets, PathComparison);
            _repositoryIgnoreStackCache[startRelativeDirectory] = new CachedIgnoreStack(_ignoreStateVersion, stack);
            return stack;
        }
    }

    internal IReadOnlyList<IgnoreRuleSet> CreateInitialRuleSets(string startRelativeDirectory)
    {
        var ruleSets = new List<IgnoreRuleSet>();
        if (GlobalExcludePath is not null &&
            TryLoadIgnoreFile(
                GlobalExcludePath,
                baseDirectory: string.Empty,
                IgnoreRuleSourceKind.GlobalExclude,
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
            ref _hasInfoExcludeState,
            ref _infoExcludeState,
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

        if (!TryLoadIgnoreFile(
            gitIgnorePath,
            baseDirectory: relativeDirectory,
            IgnoreRuleSourceKind.PerDirectory,
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
        ref bool hasState,
        ref CachedIgnoreFileState state,
        out IgnoreRuleSet ruleSet)
    {
        return TryLoadIgnoreFile(path, baseDirectory, sourceKind, key: null, ref hasState, ref state, out ruleSet);
    }

    private bool TryLoadIgnoreFile(
        string path,
        string baseDirectory,
        IgnoreRuleSourceKind sourceKind,
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

        var found = TryLoadIgnoreFile(path, baseDirectory, sourceKind, relativeDirectory, ref hasState, ref state, out ruleSet);
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
        string? key,
        ref bool hasState,
        ref CachedIgnoreFileState state,
        out IgnoreRuleSet ruleSet)
    {
        var exists = TryGetIgnoreFileMetadata(path, out var lastWriteTimeUtc, out var length);
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

        _ignoreStateVersion++;
        _repositoryIgnoreStackCache.Clear();
    }

    private readonly record struct CachedIgnoreFileState(bool Exists, DateTime LastWriteTimeUtc, long Length, IgnoreRuleSet? RuleSet);
    private readonly record struct CachedIgnoreStack(int Version, IgnoreStack Stack);
}
