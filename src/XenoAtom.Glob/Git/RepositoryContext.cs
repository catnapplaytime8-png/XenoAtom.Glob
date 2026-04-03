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
    internal RepositoryContext(string workingTreeRoot, string gitDirectory, string? globalExcludePath)
    {
        WorkingTreeRoot = workingTreeRoot;
        GitDirectory = gitDirectory;
        GlobalExcludePath = globalExcludePath;
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

    /// <summary>
    /// Gets the repository-scoped exclude file path.
    /// </summary>
    public string InfoExcludePath => Path.Combine(GitDirectory, "info", "exclude");

    internal IReadOnlyList<IgnoreRuleSet> CreateInitialRuleSets(string startRelativeDirectory)
    {
        var ruleSets = new List<IgnoreRuleSet>();
        if (GlobalExcludePath is not null && File.Exists(GlobalExcludePath))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(GlobalExcludePath),
                sourcePath: GlobalExcludePath,
                sourceKind: IgnoreRuleSourceKind.GlobalExclude));
        }

        if (File.Exists(InfoExcludePath))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(InfoExcludePath),
                sourcePath: InfoExcludePath,
                sourceKind: IgnoreRuleSourceKind.RepositoryExclude));
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

        if (!File.Exists(gitIgnorePath) || IsSymlink(gitIgnorePath))
        {
            ruleSet = null!;
            return false;
        }

        ruleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(gitIgnorePath),
            baseDirectory: relativeDirectory,
            sourcePath: gitIgnorePath,
            sourceKind: IgnoreRuleSourceKind.PerDirectory);
        return true;
    }

    private static bool IsSymlink(string path)
    {
        var attributes = File.GetAttributes(path);
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }
}
