// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.IO;

[TestClass]
public class FileTreeWalkerTests
{
    [TestMethod]
    public void Enumerate_ShouldReturnAllFilesWhenNoIgnoreRulesExist()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/lib/util.cs", string.Empty);

        var walker = new FileTreeWalker();
        var entries = walker.Enumerate(tempDirectory.Path)
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "src/app.cs", "src/lib/util.cs" }, entries);
    }

    [TestMethod]
    public void Enumerate_ShouldPruneIgnoredDirectories()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            obj/
            *.tmp
            """);
        tempDirectory.WriteAllText("src/.gitignore", """
            generated/
            !generated/include.txt
            """);

        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/generated/file.cs", string.Empty);
        tempDirectory.WriteAllText("src/generated/include.txt", string.Empty);
        tempDirectory.WriteAllText("obj/build.bin", string.Empty);
        tempDirectory.WriteAllText("root.tmp", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var entries = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore", "src/.gitignore", "src/app.cs" }, entries);
    }

    [TestMethod]
    public void Enumerate_ShouldMatchGitVisibleFileSet()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            *.log
            temp/
            """);
        tempDirectory.WriteAllText("src/.gitignore", """
            generated/
            special.txt
            """);
        tempDirectory.WriteAllText(".git/info/exclude", "ignored-by-info.txt\n");

        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/special.txt", string.Empty);
        tempDirectory.WriteAllText("src/generated/code.cs", string.Empty);
        tempDirectory.WriteAllText("temp/cache.bin", string.Empty);
        tempDirectory.WriteAllText("trace.log", string.Empty);
        tempDirectory.WriteAllText("ignored-by-info.txt", string.Empty);
        tempDirectory.WriteAllText("README.md", string.Empty);

        var allFiles = Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(tempDirectory.Path, path).Replace('\\', '/'))
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        var expected = QueryVisiblePathsFromGit(git, allFiles);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var actual = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    private static string[] QueryVisiblePathsFromGit(GitCli git, IReadOnlyList<string> paths)
    {
        var input = string.Join('\0', paths) + '\0';
        var result = git.RunCheckedWithInput(input, "check-ignore", "--no-index", "--stdin", "-z", "-v", "--non-matching");
        var tokens = result.StandardOutput.Split('\0');
        var visiblePaths = new List<string>();
        for (var index = 0; index + 3 < tokens.Length; index += 4)
        {
            var pattern = tokens[index + 2];
            var path = tokens[index + 3];
            if (path.Length == 0)
            {
                continue;
            }

            if (pattern.Length == 0 || pattern[0] == '!')
            {
                visiblePaths.Add(path);
            }
        }

        visiblePaths.Sort(StringComparer.Ordinal);
        return visiblePaths.ToArray();
    }
}
