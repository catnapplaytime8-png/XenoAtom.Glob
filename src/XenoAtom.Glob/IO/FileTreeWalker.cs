// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.IO.Enumeration;

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.IO;

/// <summary>
/// Enumerates files and directories with optional ignore handling.
/// </summary>
public sealed class FileTreeWalker
{
    /// <summary>
    /// Enumerates a directory tree according to the specified options.
    /// </summary>
    /// <param name="rootPath">The directory to traverse.</param>
    /// <param name="options">Optional traversal options.</param>
    /// <returns>A lazy sequence of file tree entries.</returns>
    public IEnumerable<FileTreeEntry> Enumerate(string rootPath, FileTreeWalkOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        options ??= new FileTreeWalkOptions();
        var fullRootPath = Path.GetFullPath(rootPath);
        var repositoryContext = options.RepositoryContext;
        IReadOnlyList<IgnoreRuleSet> rootRuleSets = options.AdditionalRuleSets ?? [];
        var startRelativeDirectory = string.Empty;

        if (repositoryContext is not null)
        {
            startRelativeDirectory = GetRelativeDirectory(repositoryContext.WorkingTreeRoot, fullRootPath);
            rootRuleSets = MergeRuleSets(
                repositoryContext.CreateInitialRuleSets(startRelativeDirectory),
                options.AdditionalRuleSets);
        }

        var rootIgnoreStack = new IgnoreStack(rootRuleSets);
        return EnumerateCore(fullRootPath, startRelativeDirectory, rootIgnoreStack, options, repositoryContext);
    }

    private IEnumerable<FileTreeEntry> EnumerateCore(
        string directoryPath,
        string relativeDirectory,
        IgnoreStack ignoreStack,
        FileTreeWalkOptions options,
        RepositoryContext? repositoryContext)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        foreach (var entry in EnumerateDirectory(directoryPath))
        {
            options.CancellationToken.ThrowIfCancellationRequested();

            if (repositoryContext is not null && entry.Name == ".git")
            {
                continue;
            }

            if (!options.FollowSymbolicLinks && entry.IsReparsePoint)
            {
                continue;
            }

            var relativePath = relativeDirectory.Length == 0 ? entry.Name : $"{relativeDirectory}/{entry.Name}";
            var ignored = ignoreStack.Matcher.Evaluate(relativePath, entry.IsDirectory);
            if (ignored.IsIgnored)
            {
                continue;
            }

            if (entry.IsDirectory)
            {
                if (options.IncludeDirectories)
                {
                    yield return new FileTreeEntry(relativePath, entry.FullPath, true);
                }

                var childIgnoreStack = ignoreStack.PushDirectory(repositoryContext, relativePath);
                foreach (var childEntry in EnumerateCore(entry.FullPath, relativePath, childIgnoreStack, options, repositoryContext))
                {
                    yield return childEntry;
                }

                continue;
            }

            yield return new FileTreeEntry(relativePath, entry.FullPath, false);
        }
    }

    private static IReadOnlyList<IgnoreRuleSet> MergeRuleSets(
        IReadOnlyList<IgnoreRuleSet> initialRuleSets,
        IReadOnlyList<IgnoreRuleSet>? additionalRuleSets)
    {
        if (additionalRuleSets is null || additionalRuleSets.Count == 0)
        {
            return initialRuleSets;
        }

        var merged = new List<IgnoreRuleSet>(initialRuleSets.Count + additionalRuleSets.Count);
        merged.AddRange(initialRuleSets);
        merged.AddRange(additionalRuleSets);
        return merged;
    }

    private static string GetRelativeDirectory(string repositoryRoot, string rootPath)
    {
        if (string.Equals(repositoryRoot, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, rootPath);
        return PathNormalizer.NormalizeRelativePath(relativePath, isDirectory: true).Value;
    }

    private static IEnumerable<RawFileSystemEntry> EnumerateDirectory(string directoryPath)
    {
        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
        };

        var enumerable = new FileSystemEnumerable<RawFileSystemEntry>(
            directoryPath,
            static (ref FileSystemEntry entry) => new RawFileSystemEntry(
                entry.FileName.ToString(),
                entry.ToFullPath(),
                entry.IsDirectory,
                (entry.Attributes & FileAttributes.ReparsePoint) != 0),
            enumerationOptions);

        foreach (var entry in enumerable)
        {
            yield return entry;
        }
    }

    private readonly record struct RawFileSystemEntry(string Name, string FullPath, bool IsDirectory, bool IsReparsePoint);
}
