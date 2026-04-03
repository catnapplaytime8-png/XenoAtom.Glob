// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Git;

/// <summary>
/// Discovers Git repository metadata for working tree paths.
/// </summary>
public static class RepositoryDiscovery
{
    /// <summary>
    /// Attempts to discover a repository context from the specified path.
    /// </summary>
    /// <param name="path">A path inside the working tree.</param>
    /// <param name="context">The discovered repository context when successful.</param>
    /// <returns><see langword="true"/> when discovery succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryDiscover(string path, out RepositoryContext? context)
    {
        ArgumentNullException.ThrowIfNull(path);

        var currentDirectory = Directory.Exists(path)
            ? Path.GetFullPath(path)
            : Path.GetDirectoryName(Path.GetFullPath(path));

        while (!string.IsNullOrEmpty(currentDirectory))
        {
            var dotGitPath = Path.Combine(currentDirectory, ".git");
            if (Directory.Exists(dotGitPath))
            {
                context = new RepositoryContext(
                    currentDirectory,
                    dotGitPath,
                    GitConfigReader.ResolveGlobalExcludePath(dotGitPath));
                return true;
            }

            if (File.Exists(dotGitPath))
            {
                var gitDirectory = ResolveGitFile(dotGitPath, currentDirectory);
                context = new RepositoryContext(
                    currentDirectory,
                    gitDirectory,
                    GitConfigReader.ResolveGlobalExcludePath(gitDirectory));
                return true;
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        context = null;
        return false;
    }

    /// <summary>
    /// Discovers a repository context from the specified path.
    /// </summary>
    /// <param name="path">A path inside the working tree.</param>
    /// <returns>The discovered repository context.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path is not inside a Git working tree.</exception>
    public static RepositoryContext Discover(string path)
    {
        if (TryDiscover(path, out var context))
        {
            return context!;
        }

        throw new InvalidOperationException($"No Git working tree was found for path '{path}'.");
    }

    private static string ResolveGitFile(string gitFilePath, string workingTreeRoot)
    {
        var content = File.ReadAllText(gitFilePath).Trim();
        const string prefix = "gitdir:";
        if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid gitfile format: '{gitFilePath}'.");
        }

        var gitDirectory = content[prefix.Length..].Trim();
        if (Path.IsPathRooted(gitDirectory))
        {
            return Path.GetFullPath(gitDirectory);
        }

        return Path.GetFullPath(Path.Combine(workingTreeRoot, gitDirectory));
    }
}
